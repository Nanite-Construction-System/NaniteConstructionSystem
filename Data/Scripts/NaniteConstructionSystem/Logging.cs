using System;
using System.Collections.Concurrent;
using System.Text;
using Sandbox.ModAPI;
using System.IO;
using VRage;
using VRage.Utils;

namespace NaniteConstructionSystem
{
    public class Logging
    {
        private static Logging m_instance;

        private TextWriter m_writer;
        private ConcurrentBag<string> m_writeCache;
        private FastResourceLock m_lock;
        private bool m_busy;
        private string m_logFile;

        static public Logging Instance
        {
            get
            {
                if (m_instance == null)
                    m_instance = new Logging("NaniteConstructionSystem.log");

                return m_instance;
            }
        }

        public Logging(string logFile)
        {
            try
            {
                m_instance = this;
                m_writeCache = new ConcurrentBag<string>();
                m_logFile = logFile;
                m_busy = false;
            }
            catch { }
        }

        public void WriteLine(string text)
        {
            MyAPIGateway.Parallel.Start(() =>
            {
                try
                    { m_writeCache.Add(DateTime.Now.ToString("[HH:mm:ss] ") + text + "\r\n"); }
                catch (Exception e)
                    { MyLog.Default.WriteLineAndConsole($"Nanite.Logging.WriteLine Error: {e.ToString()}"); }
            });
        }

        public void WriteToFile()
        { // Called once every second from the main logic in Core.cs
            MyAPIGateway.Parallel.StartBackground(() =>
            {
                try
                {
                    if (m_busy)
                        return;
                    
                    m_busy = true;
                    if (m_writer == null)
                    {
                        if (MyAPIGateway.Utilities == null)
                            return;

                        m_writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(m_logFile, typeof(Logging));
                    }

                    while (!m_writeCache.IsEmpty)
                    {
                        string line = null;
                        m_writeCache.TryTake(out line);

                        if (line != null)
                            m_writer.Write(line);
                    }
                }
                catch (Exception e)
                    { MyLog.Default.WriteLineAndConsole($"Nanite.Logging.WriteToFile Error: {e.ToString()}"); }
                finally
                    { m_busy = false; }
                
            });
        }

        internal void Close()
        {
            try
            {
                if (m_writer != null)
                {
                    m_writer.Flush();
                    m_writer.Close();
                    m_writer = null;
                }

                m_instance = null;
            }
            catch (Exception e)
            {
                { MyLog.Default.WriteLineAndConsole($"Nanite.Logging.Close Error: {e.ToString()}"); }
            }
        }
    }
}
