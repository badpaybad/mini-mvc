using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MiniMvc.Core.HttpStandard
{
    public class HttpLogger : IDisposable
    {
        static ConcurrentQueue<HttpRequestLogInfo> _logs = new ConcurrentQueue<HttpRequestLogInfo>();
        static Thread _threadWriteLog;
        static bool _isStop = false;
        static HttpLogger()
        {
            _threadWriteLog = new Thread(async () => { await LoopWriteLog(); });
            _threadWriteLog.Start();
        }

        private static async Task LoopWriteLog()
        {
            while (!_isStop)
            {
                try
                {
                    List<HttpRequestLogInfo> logs = new List<HttpRequestLogInfo>();
                    for (var i = 0; i < 100; i++)
                    {
                        if (_logs.TryDequeue(out HttpRequestLogInfo info) && info != null)
                        {
                            logs.Add(info);
                        }
                    }

                    //async write your logs by write file or db or redis or elastic search ...
                    var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                    if (Directory.Exists(dir) == false) Directory.CreateDirectory(dir);
                    var file = Path.Combine(dir, $"{DateTime.Now.ToString("yyyyMMdd_HH")}.log");

                    using (var sw = new StreamWriter(file, true))
                    {
                        await sw.WriteLineAsync(JsonConvert.SerializeObject(logs));
                        sw.Flush();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                finally
                {
                    await Task.Delay(1000);
                }
            }
        }

        public static void Log(HttpRequest request)
        {
            //use queue to async write log            
            _logs.Enqueue(new HttpRequestLogInfo(request));
        }

        public void Dispose()
        {
            _isStop = true;
        }
    }

    public class HttpRequestLogInfo
    {

        public readonly HttpRequest Request;
        public readonly DateTime CreatedAt;

        public HttpRequestLogInfo(HttpRequest request)
        {
            Request = request;
            CreatedAt = DateTime.Now;
        }
    }

}
