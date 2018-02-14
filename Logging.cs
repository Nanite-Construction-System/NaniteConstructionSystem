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
                using (m_lock.AcquireExclusiveUsing())
                {
                    m_writeCache.Append(DateTime.Now.ToString("[HH:mm:ss] ") + text + "\r\n");
                }            

                if(m_writer == null)
                {
                    if (MyAPIGateway.Utilities == null)
                        return;

                    m_writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(m_logFile, typeof(Logging));
                }

                MyAPIGateway.Parallel.StartBackground(() =>
                {
                    if (m_busy)
                        return;

                    if (m_lock == null)
                        return;

                    try
                    {
                        m_busy = true;
                        string cache;
                        using (m_lock.AcquireExclusiveUsing())
                        {
                            cache = m_writeCache.ToString();
                            m_writeCache.Clear();
                        }

                        m_writer.Write(cache);
                        m_writer.Flush();
                    }
                    catch { }
                    finally
                    {
                        m_busy = false;
                    }
                });
            }
            catch(Exception ex)
            {
                MyLog.Default.WriteLine(string.Format("Logging.WriteLine Error: {0}", ex.ToString()));
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
