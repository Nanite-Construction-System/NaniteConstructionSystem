using System.Collections.Generic;
using Sandbox.ModAPI;

namespace NaniteConstructionSystem.Entities.Tools
{
    public class NaniteToolManager
    {
        private List<NaniteToolBase> m_tools;
        public List<NaniteToolBase> Tools
        {
            get { return m_tools; }
        }

        public NaniteToolManager()
        {
            m_tools = new List<NaniteToolBase>();
        }

        public void Update()
        {
            foreach (var item in m_tools)
            {
                item.Update();
            }

            for (int r = m_tools.Count - 1; r >= 0; r--)
            {
                var item = m_tools[r];
                if (MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds - item.StartTime > 1800000)
                {
                    item.Close();
                    m_tools.RemoveAt(r);
                }
            }
        }

        public void Remove(object obj)
        {
            for (int r = m_tools.Count - 1; r >= 0; r--)
            {
                var tool = m_tools[r];

                if (tool.TargetBlock == obj)
                {
                    tool.Close();
                    m_tools.RemoveAt(r);
                }
            }
        }
    }
}
