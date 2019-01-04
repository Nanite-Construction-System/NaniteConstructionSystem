using System;
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
        private StringBuilder m_writeCache;
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
                m_writeCache = new StringBuilder();
                m_lock = new FastResourceLock();
                m_logFile = logFile;
                m_busy = false;
            }
            catch { }
        }

        public void WriteLine(string text)
        {
            try
            {
                if (m_writer == null)
                {
                    if (MyAPIGateway.Utilities == null)
                        return;

                    m_writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(m_logFile, typeof(Logging));
                }

                MyAPIGateway.Parallel.StartBackground(() =>
                { // invocation 0
                    try
                    {
                        lock (m_writer)
                        {
                            m_writer.Write(DateTime.Now.ToString("[HH:mm:ss] ") + text + "\r\n");
                            m_writer.Flush();
                        }
                    }
                    catch (Exception e)
                        { MyLog.Default.WriteLineAndConsole($"Logging.WriteLine Error (invocation 0): {e.ToString()}"); }
                });
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLineAndConsole($"Logging.WriteLine Error: {e.ToString()}");
            }
        }

        internal void Close()
        {
            try
            {
                if (m_writer != null)
                {
                    if (m_writeCache.Length > 0)
                        m_writer.WriteLine(m_writeCache);

                    m_writer.Flush();
                    m_writer.Close();
                    m_writer = null;
                }

                m_instance = null;
                if (m_lock != null)
                {
                    m_lock.ReleaseExclusive();
                    m_lock = null;
                }
            }
            catch { }
        }
    }
}
