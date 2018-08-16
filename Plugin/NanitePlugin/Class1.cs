using NaniteConstructionSystem.Entities.Detectors;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using VRage.Collections;
using VRage.Game.ModAPI;
using VRage.Plugins;
using VRage.Utils;

namespace NanitePlugin
{
    public class Plugin : IPlugin
    {
        public MyConcurrentDictionary<long, IMyFunctionalBlock> facilityControl = new MyConcurrentDictionary<long, IMyFunctionalBlock>();
        public MyConcurrentDictionary<long, IMyFunctionalBlock> bigOreDetectors = new MyConcurrentDictionary<long, IMyFunctionalBlock>();
        public MyConcurrentDictionary<long, IMyFunctionalBlock> beacons = new MyConcurrentDictionary<long, IMyFunctionalBlock>();

        private Thread _serverThread;
        private readonly HttpListener _listener = new HttpListener();

        public void Dispose()
        {
            _serverThread.Abort();
            _listener.Stop();
            _listener.Close();
        }

        public void Init(object gameInstance)
        {
            _listener.Prefixes.Add("http://*:3000/");
            _listener.Start();

            foreach (var entity in MyEntities.GetEntities().Where((x) => (x as IMyCubeGrid) != null))
            {
                IMyCubeGrid grid = entity as IMyCubeGrid;
                grid.OnBlockAdded += OnBlockAdded;
                grid.OnBlockRemoved += OnBlockRemoved;
                MyLog.Default.WriteLineAndConsole($"IMyCubeGrid: {grid}");

                List<IMySlimBlock> blocks = new List<IMySlimBlock>();
                grid.GetBlocks(blocks);
                foreach (var block in blocks.Where((x) => (x as IMyTerminalBlock) != null))
                {
                    IMyTerminalBlock terminal = block as IMyTerminalBlock;
                    switch (terminal.BlockDefinition.SubtypeName)
                    {
                        case "LargeNaniteAreaBeacon":
                            MyLog.Default.WriteLineAndConsole($"LargeNaniteAreaBeacon: {terminal}");
                            break;
                        case "BigNaniteOreDetector":
                            MyLog.Default.WriteLineAndConsole($"BigNaniteOreDetector: {terminal}");
                            break;
                        case "LargeNaniteFactory":
                            MyLog.Default.WriteLineAndConsole($"LargeNaniteFactory: {terminal}");
                            break;
                    }
                }
            }

            _serverThread = new Thread(Listen);
            _serverThread.Start();
        }

        private void OnBlockAdded(IMySlimBlock obj)
        {
            MyLog.Default.WriteLineAndConsole($"OnBlockAdded: {obj}");
            if (obj.FatBlock == null)
                return;

            IMyFunctionalBlock terminal = obj.FatBlock as IMyFunctionalBlock;
            if (terminal == null)
                return;

            MyLog.Default.WriteLineAndConsole($"SubtypeName: {terminal.BlockDefinition.SubtypeName}");

            switch (terminal.BlockDefinition.SubtypeName)
            {
                case "LargeNaniteAreaBeacon":
                    beacons.Add(terminal.EntityId, terminal);
                    break;
                case "LargeNaniteBeaconDeconstruct":
                    beacons.Add(terminal.EntityId, terminal);
                    break;
                case "SmallNaniteBeaconDeconstruct":
                    beacons.Add(terminal.EntityId, terminal);
                    break;
                case "LargeNaniteBeaconConstruct":
                    beacons.Add(terminal.EntityId, terminal);
                    break;
                case "SmallNaniteBeaconConstruct":
                    beacons.Add(terminal.EntityId, terminal);
                    break;
                case "LargeNaniteBeaconProjection":
                    beacons.Add(terminal.EntityId, terminal);
                    break;
                case "SmallNaniteBeaconProjection":
                    beacons.Add(terminal.EntityId, terminal);
                    break;
                case "BigNaniteOreDetector":
                    bigOreDetectors.Add(terminal.EntityId, terminal);
                    break;
                case "LargeNaniteControlFacility":
                    facilityControl.Add(terminal.EntityId, terminal);
                    break;
            }
        }

        private void OnBlockRemoved(IMySlimBlock obj)
        {
            IMyFunctionalBlock terminal = obj.FatBlock as IMyFunctionalBlock;
            if (terminal == null)
                return;

            if (beacons.Remove(terminal.EntityId))
                return;
            if (bigOreDetectors.Remove(terminal.EntityId))
                return;
            if (facilityControl.Remove(terminal.EntityId))
                return;
        }

        public void Listen()
        {
            ThreadPool.QueueUserWorkItem((o) =>
            {
                Console.WriteLine("Webserver running...");
                try
                {
                    while (_listener.IsListening)
                    {
                        ThreadPool.QueueUserWorkItem((c) =>
                        {
                            var ctx = c as HttpListenerContext;
                            try
                            {
                                StringBuilder asd = new StringBuilder();
                                asd.Append($"bigOreDetectors:\n");
                                foreach (var detector in bigOreDetectors)
                                {
                                    MyLog.Default.WriteLineAndConsole($"GameLogic: {detector.Value.GameLogic}");
                                    BigNaniteOreDetectorLogic logic = detector.Value.GameLogic.GetAs<BigNaniteOreDetectorLogic>();
                                    MyLog.Default.WriteLineAndConsole((logic != null).ToString());
                                    asd.Append($"- EntityId={detector.Key} Enabled={detector.Value.Enabled} Range={logic.Detector.Range} MaxRange={logic.Detector.MaxRange} Power={logic.Detector.Power} ScanDuration={logic.Detector.ScanDuration}\n");
                                }
                                
                                byte[] buf = Encoding.UTF8.GetBytes(asd.ToString());
                                ctx.Response.ContentLength64 = buf.Length;
                                ctx.Response.OutputStream.Write(buf, 0, buf.Length);
                            }
                            catch (Exception ex)
                            {
                                MyLog.Default.WriteLineAndConsole(ex.ToString());
                            } // suppress any exceptions
                            finally
                            {
                                // always close the stream
                                ctx.Response.OutputStream.Close();
                            }
                        }, _listener.GetContext());
                    }
                }
                catch { } // suppress any exceptions
            });
        }

        public void Update()
        {
        }
    }
}
