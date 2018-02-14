using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRageMath;
//using Ingame = VRage.Game.ModAPI.Ingame;
using Ingame = VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI;
using Sandbox.Game.Entities;

namespace NaniteConstructionSystem.Entities.Tools
{
    public class NaniteGrinder : NaniteToolBase
    {
        private const string m_grinderGrid = @"
    <MyObjectBuilder_CubeGrid xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
      <SubtypeName />
      <EntityId>0</EntityId>
      <PersistentFlags>CastShadows InScene</PersistentFlags>
      <PositionAndOrientation>
        <Position x=""32.19761639092394"" y=""6.08317355431609"" z=""-19.344142400810597"" />
        <Forward x=""0.9271675"" y=""0.325059682"" z=""-0.186270371"" />
        <Up x=""0.229297861"" y=""-0.0991615"" z=""0.968292058"" />
        <Orientation>
          <X>0.549812</X>
          <Y>-0.323375285</Y>
          <Z>-0.4972801</Z>
          <W>0.588088155</W>
        </Orientation>
      </PositionAndOrientation>
      <GridSizeEnum>Small</GridSizeEnum>
      <CubeBlocks>
        <MyObjectBuilder_CubeBlock xsi:type=""MyObjectBuilder_Reactor"">
          <SubtypeName>SmallBlockSmallGenerator</SubtypeName>
          <EntityId>0</EntityId>
          <Min x=""1"" y=""5"" z=""0"" />
          <ComponentContainer>
            <Components>
              <ComponentData>
                <TypeId>MyInventoryBase</TypeId>
                <Component xsi:type=""MyObjectBuilder_Inventory"">
                  <Items>
                    <MyObjectBuilder_InventoryItem>
                      <Amount>3</Amount>
                      <Scale>1</Scale>
                      <PhysicalContent xsi:type=""MyObjectBuilder_Ingot"">
                        <SubtypeName>Uranium</SubtypeName>
                        <DurabilityHP xsi:nil=""true"" />
                      </PhysicalContent>
                      <ItemId>0</ItemId>
                      <AmountDecimal>3</AmountDecimal>
                    </MyObjectBuilder_InventoryItem>
                  </Items>
                  <nextItemId>0</nextItemId>
                  <Volume>0.125</Volume>
                  <Mass>9223372036854.775807</Mass>
                  <Size xsi:nil=""true"" />
                  <InventoryFlags>CanReceive</InventoryFlags>
                  <RemoveEntityOnEmpty>false</RemoveEntityOnEmpty>
                </Component>
              </ComponentData>
            </Components>
          </ComponentContainer>
          <CustomName>Small Reactor 2</CustomName>
          <ShowOnHUD>false</ShowOnHUD>
          <ShowInTerminal>true</ShowInTerminal>
          <ShowInToolbarConfig>true</ShowInToolbarConfig>
          <Enabled>true</Enabled>
          <Inventory>
            <Items>
              <MyObjectBuilder_InventoryItem>
                <Amount>3</Amount>
                <Scale>1</Scale>
                <PhysicalContent xsi:type=""MyObjectBuilder_Ingot"">
                  <SubtypeName>Uranium</SubtypeName>
                  <DurabilityHP xsi:nil=""true"" />
                </PhysicalContent>
                <ItemId>0</ItemId>
                <AmountDecimal>3</AmountDecimal>
              </MyObjectBuilder_InventoryItem>
            </Items>
            <nextItemId>0</nextItemId>
            <Volume>0.125</Volume>
            <Mass>9223372036854.775807</Mass>
            <Size xsi:nil=""true"" />
            <InventoryFlags>CanReceive</InventoryFlags>
            <RemoveEntityOnEmpty>false</RemoveEntityOnEmpty>
          </Inventory>
        </MyObjectBuilder_CubeBlock>
        <MyObjectBuilder_CubeBlock xsi:type=""MyObjectBuilder_ShipGrinder"">
          <SubtypeName>{0}NaniteShipGrinder</SubtypeName>
          <EntityId>0</EntityId>
          <Min x=""1"" y=""-1"" z=""0"" />
          <BlockOrientation Forward=""Down"" Up=""Backward"" />
          <DeformationRatio>0.5</DeformationRatio>
          <ComponentContainer>
            <Components>
              <ComponentData>
                <TypeId>MyInventoryBase</TypeId>
                <Component xsi:type=""MyObjectBuilder_Inventory"">
                  <Items />
                  <nextItemId>0</nextItemId>
                  <Volume>1000</Volume>
                  <Mass>9223372036854.775807</Mass>
                  <Size xsi:nil=""true"" />
                  <InventoryFlags>CanSend</InventoryFlags>
                  <RemoveEntityOnEmpty>false</RemoveEntityOnEmpty>
                </Component>
              </ComponentData>
            </Components>
          </ComponentContainer>
          <ShowOnHUD>false</ShowOnHUD>
          <ShowInTerminal>true</ShowInTerminal>
          <ShowInToolbarConfig>true</ShowInToolbarConfig>
          <Enabled>false</Enabled>
          <Inventory>
            <Items />
            <nextItemId>0</nextItemId>
            <Volume>3.375</Volume>
            <Mass>9223372036854.775807</Mass>
            <Size xsi:nil=""true"" />
            <InventoryFlags>CanSend</InventoryFlags>
            <RemoveEntityOnEmpty>false</RemoveEntityOnEmpty>
          </Inventory>
        </MyObjectBuilder_CubeBlock>
      </CubeBlocks>
      <DisplayName>SmallNaniteToolCube</DisplayName>
      <DestructibleBlocks>true</DestructibleBlocks>
      <IsRespawnGrid>false</IsRespawnGrid>
      <LocalCoordSys>0</LocalCoordSys>
    </MyObjectBuilder_CubeGrid>";

        public NaniteGrinder(NaniteConstructionBlock constructionBlock, IMySlimBlock block, int waitTime, bool performanceFriendly) : base(constructionBlock, block, waitTime, m_grinderGrid, performanceFriendly, true)
        {
        }
    }
}
