using System;
using Sandbox.ModAPI;
using VRageMath;
using VRage;

using NaniteConstructionSystem.Entities;
using NaniteConstructionSystem.Settings;

namespace NaniteConstructionSystem.Extensions
{
    public static class Sync
    {
        public static bool IsServer
        {
            get
            {
                if (MyAPIGateway.Session == null)
                    return false;

                if (MyAPIGateway.Session.OnlineMode == VRage.Game.MyOnlineModeEnum.OFFLINE || MyAPIGateway.Multiplayer.IsServer)
                    return true;

                return false;
            }
        }

        public static bool IsClient
        {
            get
            {
                if (MyAPIGateway.Session == null)
                    return false;

                if (MyAPIGateway.Session.OnlineMode == VRage.Game.MyOnlineModeEnum.OFFLINE)
                    return true;

                if (MyAPIGateway.Session.Player != null && MyAPIGateway.Session.Player.Client != null && MyAPIGateway.Multiplayer.IsServerPlayer(MyAPIGateway.Session.Player.Client))
                    return true;

                if (!MyAPIGateway.Multiplayer.IsServer)
                    return true;

                return false;
            }
        }

        public static bool IsDedicated
        {
            get
            {
                if (MyAPIGateway.Utilities.IsDedicated)
                    return true;

                return false;
            }
        }
    }

    public class StateData
    {
        public long EntityId { get; set; }
        public NaniteConstructionBlock.FactoryStates State { get; set; }
    }

    /*
    I have a bug report of clients crashing:
    2016-04-04 18:17:33.837 - Thread: 1 -> Exception occured: System.InvalidOperationException: Unable to generate a temporary class (result=1).
    error CS0012: The type 'VRageMath.Vector3I' is defined in an assembly that is not referenced. You must add a reference to assembly 'VRage.Math, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null'.

    I have no idea wtf is going on?  TargetData used to use SerializableVector3I as a position, but apparently this fails ... sometimes?  I have no idea why.  So
    I guess I will unwrap position.  UGLY.
            */

    public enum TargetTypes
    {
        Construction,
        Projection,
        Floating,
        Deconstruction,
        Voxel,
        Medical
    }

    [Serializable]
    public class TargetVector3I
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }

        public TargetVector3I()
        {
            X = 0;
            Y = 0;
            Z = 0;
        }

        public TargetVector3I(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static implicit operator TargetVector3I(Vector3I position)
        {
            return new TargetVector3I(position.X, position.Y, position.Z);
        }

        public override string ToString()
        {
            return string.Format("(X:{0},Y:{1},Z:{2})", X, Y, Z);
        }
    }

    [Serializable]
    public class TargetVector3D
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public TargetVector3D()
        {
            X = 0f;
            Y = 0f;
            Z = 0f;
        }

        public TargetVector3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static implicit operator TargetVector3D(Vector3D position)
        {
            return new TargetVector3D(position.X, position.Y, position.Z);
        }

        public override string ToString()
        {
            return string.Format("(X:{0},Y:{1},Z:{2})", X, Y, Z);
        }
    }

    [Serializable]
    public class TargetData
    {
        public long EntityId { get; set; }
        public long TargetId { get; set; }
        public TargetVector3I PositionI { get; set; }
        public TargetVector3D PositionD { get; set; }
        public TargetTypes TargetType { get; set; }
        public long SubTargetId { get; set; }

        public override string ToString()
        {
            return string.Format("EntityId={0},TargetId={1},SubTargetId={2},TargetType={3},PositionI={4},PositionD={5}", EntityId, TargetId, SubTargetId, TargetType, PositionI, PositionD);
        }
    }

    [Serializable]
    public class DetailData
    {
        public long EntityId { get; set; }
        public string Details { get; set; }
    }

    [Serializable]
    public class LoginData
    {
        public ulong SteamId { get; set; }
    }

    [Serializable]
    public class SettingsData
    {
        public NaniteSettings Settings { get; set; }
    }

    [Serializable]
    public class ParticleData
    {
        public long EntityId { get; set; }
        public long TargetId { get; set; }
        public int PositionX { get; set; }
        public int PositionY { get; set; }
        public int PositionZ { get; set; }
        public int EffectId { get; set; }

    }

    [Serializable]
    public class VoxelRemovalData
    {
        public long VoxelID { get; set; }
        public Vector3D Position { get; set; }
    }

}
