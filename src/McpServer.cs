using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace ScreenInk
{
    internal static class McpServer
    {
        private const string Bridge = "http://127.0.0.1:3212";
        private const string ProtocolVersion = "2024-11-05";

        public static void Run()
        {
            try { Console.InputEncoding = Encoding.UTF8; Console.OutputEncoding = Encoding.UTF8; } catch { }
            JavaScriptSerializer ser = new JavaScriptSerializer();
            string line;
            while ((line = Console.ReadLine()) != null)
            {
                if (String.IsNullOrWhiteSpace(line)) continue;
                Dictionary<string, object> req = null;
                try { req = ser.Deserialize<Dictionary<string, object>>(line); } catch { continue; }
                if (req == null) continue;
                string method = req.ContainsKey("method") ? Convert.ToString(req["method"]) : "";
                object id = req.ContainsKey("id") ? req["id"] : null;
                if (String.IsNullOrEmpty(method)) continue;
                if (method.StartsWith("notifications/")) continue;
                try
                {
                    if (method == "initialize")
                        Respond(id, ser.Serialize(new Dictionary<string, object> {
                            { "protocolVersion", ProtocolVersion },
                            { "capabilities", new Dictionary<string, object> {
                                { "tools", new Dictionary<string, object>() },
                                { "resources", new Dictionary<string, object> { { "subscribe", true }, { "listChanged", false } } }
                            } },
                            { "serverInfo", new Dictionary<string, object> { { "name", "screenink" }, { "version", "0.1.0-alpha" } } }
                        }));
                    else if (method == "ping") Respond(id, "{}");
                    else if (method == "tools/list") Respond(id, ser.Serialize(new Dictionary<string, object> { { "tools", Tools() } }));
                    else if (method == "tools/call") Respond(id, CallTool(req, ser));
                    else if (method == "resources/list") Respond(id, ser.Serialize(new Dictionary<string, object> { { "resources", Resources() } }));
                    else if (method == "resources/read") Respond(id, ReadResource(req, ser));
                    else if (method == "resources/subscribe") Respond(id, "{}");
                    else RespondError(id, -32601, "Method not found: " + method);
                }
                catch (Exception e) { RespondError(id, -32603, e.Message); }
            }
        }

        private static object[] Tools()
        {
            return new object[] {
                Tool("screenink_get_context", "获取当前 ScreenInk 目标窗口、工具、最近标注和启发式意图。", new Dictionary<string,object>()),
                Tool("screenink_get_annotations", "获取最近的屏幕标注记录（JSONL 本地记录）。", new Dictionary<string,object>()),
                Tool("screenink_get_events", "获取 ScreenInk 最近操作事件。", new Dictionary<string,object>()),
                Tool("screenink_get_target", "获取当前目标窗口和边界。", new Dictionary<string,object>()),
                Tool("screenink_get_intent", "获取基于标注文本/工具/窗口标题的启发式意图提示。", new Dictionary<string,object>())
            };
        }

        private static Dictionary<string, object> Tool(string name, string description, Dictionary<string, object> properties)
        {
            Dictionary<string, object> schema = new Dictionary<string, object>();
            schema["type"] = "object";
            schema["properties"] = properties;
            schema["additionalProperties"] = false;
            Dictionary<string, object> t = new Dictionary<string, object>();
            t["name"] = name;
            t["description"] = description;
            t["inputSchema"] = schema;
            return t;
        }

        private static object[] Resources()
        {
            return new object[] {
                Resource("screenink://context", "当前目标、工具、最近标注和意图"),
                Resource("screenink://annotations", "最近标注记录"),
                Resource("screenink://events", "最近操作事件")
            };
        }

        private static Dictionary<string, object> Resource(string uri, string description)
        {
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["uri"] = uri;
            r["name"] = uri.Replace("screenink://", "");
            r["description"] = description;
            r["mimeType"] = "application/json";
            return r;
        }

        private static string CallTool(Dictionary<string, object> req, JavaScriptSerializer ser)
        {
            Dictionary<string, object> p = req.ContainsKey("params") ? req["params"] as Dictionary<string, object> : null;
            string name = p != null && p.ContainsKey("name") ? Convert.ToString(p["name"]) : "";
            string path;
            if (name == "screenink_get_context") path = "/context";
            else if (name == "screenink_get_annotations") path = "/annotations";
            else if (name == "screenink_get_events") path = "/events";
            else if (name == "screenink_get_target" || name == "screenink_get_intent") path = "/context";
            else return ser.Serialize(new Dictionary<string, object> { { "content", new object[] { Text("未知工具：" + name) } }, { "isError", true } });
            string body = Http(path);
            if (body == null) return ser.Serialize(new Dictionary<string, object> { { "content", new object[] { Text("ScreenInk GUI 未运行或 3212 桥不可用。") } }, { "isError", true } });
            if (name == "screenink_get_target" || name == "screenink_get_intent")
            {
                Dictionary<string, object> ctx = ser.Deserialize<Dictionary<string, object>>(body);
                object part = name == "screenink_get_target" ? ctx["target"] : ctx["intent"];
                body = ser.Serialize(part);
            }
            return ser.Serialize(new Dictionary<string, object> { { "content", new object[] { Text(body) } }, { "isError", false } });
        }

        private static Dictionary<string, object> Text(string text)
        {
            return new Dictionary<string, object> { { "type", "text" }, { "text", text } };
        }

        private static string ReadResource(Dictionary<string, object> req, JavaScriptSerializer ser)
        {
            Dictionary<string, object> p = req.ContainsKey("params") ? req["params"] as Dictionary<string, object> : null;
            string uri = p != null && p.ContainsKey("uri") ? Convert.ToString(p["uri"]) : "";
            string path = uri == "screenink://context" ? "/context" : (uri == "screenink://annotations" ? "/annotations" : (uri == "screenink://events" ? "/events" : null));
            if (path == null) return ser.Serialize(new Dictionary<string, object> { { "contents", new object[0] } });
            string body = Http(path);
            if (body == null) return ser.Serialize(new Dictionary<string, object> { { "contents", new object[0] } });
            return ser.Serialize(new Dictionary<string, object> { { "contents", new object[] {
                new Dictionary<string, object> { { "uri", uri }, { "mimeType", "application/json" }, { "text", body } }
            } } });
        }

        private static string Http(string path)
        {
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(Bridge + path);
                req.Timeout = 2500;
                using (HttpWebResponse res = (HttpWebResponse)req.GetResponse())
                using (StreamReader r = new StreamReader(res.GetResponseStream(), Encoding.UTF8))
                    return r.ReadToEnd();
            }
            catch { return null; }
        }

        private static void Respond(object id, string resultJson)
        {
            Console.WriteLine("{\"jsonrpc\":\"2.0\",\"id\":" + JsonId(id) + ",\"result\":" + resultJson + "}");
            Console.Out.Flush();
        }

        private static void RespondError(object id, int code, string message)
        {
            JavaScriptSerializer ser = new JavaScriptSerializer();
            Console.WriteLine("{\"jsonrpc\":\"2.0\",\"id\":" + JsonId(id) + ",\"error\":{\"code\":" + code + ",\"message\":" + ser.Serialize(message) + "}}");
            Console.Out.Flush();
        }

        private static string JsonId(object id)
        {
            if (id == null) return "null";
            if (id is int || id is long || id is double) return Convert.ToString(id, System.Globalization.CultureInfo.InvariantCulture);
            return new JavaScriptSerializer().Serialize(id);
        }
    }
}
