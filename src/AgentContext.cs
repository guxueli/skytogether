using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace ScreenInk
{
    internal static class ActivityLog
    {
        private static readonly object Gate = new object();
        private static readonly List<Dictionary<string, object>> Items = new List<Dictionary<string, object>>();

        public static void Add(string type, string detail)
        {
            lock (Gate)
            {
                Dictionary<string, object> e = new Dictionary<string, object>();
                e["at"] = DateTime.UtcNow.ToString("o");
                e["type"] = type ?? "";
                e["detail"] = detail ?? "";
                Items.Insert(0, e);
                if (Items.Count > 200) Items.RemoveRange(200, Items.Count - 200);
            }
        }

        public static List<object> Recent(int max)
        {
            lock (Gate)
            {
                List<object> outList = new List<object>();
                for (int i = 0; i < Items.Count && i < max; i++) outList.Add(Items[i]);
                return outList;
            }
        }
    }

    internal static class IntentEngine
    {
        public static Dictionary<string, object> Infer(string targetTitle, string targetApp, string tool, bool active, List<object> recent)
        {
            List<string> texts = new List<string>();
            int notes = 0;
            int boxes = 0;
            int lines = 0;
            foreach (object item in recent)
            {
                Dictionary<string, object> d = item as Dictionary<string, object>;
                if (d == null) continue;
                string text = d.ContainsKey("text") ? Convert.ToString(d["text"]) : "";
                string t = d.ContainsKey("tool") ? Convert.ToString(d["tool"]) : "";
                if (!String.IsNullOrEmpty(text)) { texts.Add(text); notes++; }
                if (t == "box") boxes++;
                else if (t == "line" || t == "arrow" || t == "freehand" || t == "polyline") lines++;
            }
            string joined = String.Join(" ", texts).ToLowerInvariant();
            string title = (targetTitle ?? "").ToLowerInvariant();
            List<string> tags = new List<string>();
            string summary;
            double confidence = 0.55;
            if (ContainsAny(joined, new string[] { "问题", "错误", "bug", "失败", "不对", "异常", "崩溃", "修复" }))
            {
                tags.Add("issue"); tags.Add("debug"); summary = "用户可能在指出问题或缺陷，想让智能体定位/修复"; confidence = 0.78;
            }
            else if (ContainsAny(joined, new string[] { "建议", "应该", "优化", "改", "调整", "换", "重做", "删除", "不要" }))
            {
                tags.Add("change"); tags.Add("feedback"); summary = "用户可能在提出修改意见或验收反馈"; confidence = 0.74;
            }
            else if (ContainsAny(joined, new string[] { "重点", "注意", "讲解", "演示", "这里", "步骤", "流程" }))
            {
                tags.Add("explain"); tags.Add("highlight"); summary = "用户可能在讲解或强调某个区域"; confidence = 0.7;
            }
            else if (ContainsAny(title, new string[] { "直播", "视频", "会议", "演示", "课堂", "播放" }))
            {
                tags.Add("presentation"); tags.Add("explain"); summary = "用户可能在做直播/录屏讲解，用标注辅助说明"; confidence = 0.66;
            }
            else if (boxes >= 2)
            {
                tags.Add("review"); tags.Add("regions"); summary = "用户可能在圈选多个区域做对比或评审"; confidence = 0.62;
            }
            else if (active && (lines > 0 || boxes > 0))
            {
                tags.Add("visual-note"); summary = "用户正在做视觉标记，意图需要结合目标程序上下文判断"; confidence = 0.5;
            }
            else
            {
                tags.Add("explore"); summary = "暂无足够标注，可能是在选择目标或准备标注"; confidence = 0.35;
            }
            Dictionary<string, object> intent = new Dictionary<string, object>();
            intent["summary"] = summary;
            intent["tags"] = tags;
            intent["confidence"] = Math.Round(confidence, 2);
            intent["evidence"] = texts.GetRange(0, Math.Min(texts.Count, 3));
            return intent;
        }

        private static bool ContainsAny(string text, string[] words)
        {
            foreach (string w in words) if (text.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        public static List<string> SuggestedPrompts()
        {
            return new List<string> { "请根据我最近的屏幕标注，总结我想解决的问题。", "请把我的标注整理成可执行的修改清单。", "请判断这些标注是在讲解、评审还是报错。" };
        }
    }

    internal class AgentBridge
    {
        private readonly Func<Dictionary<string, object>> _provider;
        private TcpListener _listener;
        private volatile bool _running;
        public int Port = 3212;

        public AgentBridge(Func<Dictionary<string, object>> provider) { _provider = provider; }

        public bool Start(out string error)
        {
            error = null;
            try
            {
                _listener = new TcpListener(IPAddress.Loopback, Port);
                _listener.Start();
                _running = true;
                Thread t = new Thread(Loop);
                t.IsBackground = true;
                t.Start();
                ActivityLog.Add("agent_bridge", "http://127.0.0.1:" + Port + "/context");
                return true;
            }
            catch (Exception e) { error = e.Message; return false; }
        }

        public void Stop()
        {
            _running = false;
            try { if (_listener != null) _listener.Stop(); } catch { }
        }

        private void Loop()
        {
            while (_running)
            {
                try
                {
                    TcpClient c = _listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(Handle, c);
                }
                catch { if (!_running) return; }
            }
        }

        private void Handle(object state)
        {
            TcpClient client = state as TcpClient;
            if (client == null) return;
            try
            {
                using (client)
                using (NetworkStream ns = client.GetStream())
                {
                    StreamReader reader = new StreamReader(ns, Encoding.ASCII, false, 1024, true);
                    string line = reader.ReadLine();
                    if (String.IsNullOrEmpty(line)) return;
                    string[] parts = line.Split(' ');
                    if (parts.Length < 2) return;
                    string method = parts[0].ToUpperInvariant();
                    string path = parts[1].Split('?')[0];
                    string body;
                    int status = 200;
                    if (method == "OPTIONS") { Write(ns, 204, "{}"); return; }
                    if (method != "GET") { status = 405; body = "{\"ok\":false,\"error\":\"GET only\"}"; }
                    else if (path == "/health")
                        body = "{\"ok\":true,\"app\":\"ScreenInk\",\"api\":\"http://127.0.0.1:" + Port + "/context\"}";
                    else if (path == "/context")
                    {
                        Dictionary<string, object> ctx = _provider != null ? _provider() : new Dictionary<string, object>();
                        body = new JavaScriptSerializer().Serialize(ctx);
                    }
                    else if (path == "/events")
                        body = new JavaScriptSerializer().Serialize(new Dictionary<string, object> { { "ok", true }, { "items", ActivityLog.Recent(50) } });
                    else if (path == "/annotations")
                        body = new JavaScriptSerializer().Serialize(new Dictionary<string, object> { { "ok", true }, { "items", LocalStore.Recent(50) } });
                    else { status = 404; body = "{\"ok\":false,\"error\":\"not found\"}"; }
                    Write(ns, status, body);
                }
            }
            catch { }
        }

        private static void Write(NetworkStream ns, int status, string body)
        {
            byte[] data = Encoding.UTF8.GetBytes(body ?? "{}");
            StringBuilder h = new StringBuilder();
            h.Append("HTTP/1.1 ").Append(status).Append(status == 200 ? " OK" : (status == 204 ? " No Content" : (status == 404 ? " Not Found" : " Error"))).Append("\r\n");
            h.Append("Content-Type: application/json; charset=utf-8\r\n");
            h.Append("Access-Control-Allow-Origin: *\r\n");
            h.Append("Access-Control-Allow-Methods: GET,OPTIONS\r\n");
            h.Append("Access-Control-Allow-Headers: Content-Type\r\n");
            h.Append("Cache-Control: no-store\r\n");
            h.Append("Content-Length: ").Append(data.Length).Append("\r\n");
            h.Append("Connection: close\r\n\r\n");
            byte[] head = Encoding.ASCII.GetBytes(h.ToString());
            ns.Write(head, 0, head.Length);
            if (status != 204) ns.Write(data, 0, data.Length);
        }
    }
}
