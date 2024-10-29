// <copyright file="FindBrokenNodesSystem.cs" company="Yenyang's Mods. MIT License">
// Copyright (c) Yenyang's Mods. MIT License. All rights reserved.
// </copyright>
namespace BrokenSegmentDetector.Systems
{
    using Colossal.Entities;
    using Colossal.Logging;
    using Colossal.Serialization.Entities;
    using Game;
    using Game.Common;
    using Game.Prefabs;
    using Game.Simulation;
    using Game.Tools;
    using Unity.Collections;
    using Unity.Entities;

    /// <summary>
    /// Finds broken nodes that are missing electricity and water network data.
    /// </summary>
    public partial class FindBrokenNodesSystem : GameSystemBase
    {
        private EntityQuery m_BrokenNodesQuery;
        private PrefabSystem m_PrefabSystem;
        private EndFrameBarrier m_EndFrameBarrier;
        private ILog m_Log;
#if DELETE_NO_CONNECTED_EDGE
        private EntityQuery m_AllEdgesQuery;
#endif

        /// <inheritdoc/>
        protected override void OnCreate()
        {
            base.OnCreate();
            m_Log = Mod.log;
            m_Log.Info($"{nameof(FindBrokenNodesSystem)}.{nameof(OnCreate)}");
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_EndFrameBarrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();
            m_BrokenNodesQuery = SystemAPI.QueryBuilder()
                 .WithAll<Game.Net.Node, PrefabRef, Game.Net.ConnectedEdge>()
                 .WithNone<Game.Simulation.ElectricityNodeConnection, Game.Simulation.WaterPipeNodeConnection, Game.Net.Marker, Game.Tools.EditorContainer, Game.Net.Waterway>()
                 .Build();

#if DELETE_NO_CONNECTED_EDGE
            m_AllEdgesQuery = SystemAPI.QueryBuilder()
                .WithAll<Game.Net.Edge>()
                .Build();
#endif

            Enabled = false;
        }

        /// <inheritdoc/>
        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);

            EntityCommandBuffer buffer = m_EndFrameBarrier.CreateCommandBuffer();

#if DELETE_NO_CONNECTED_EDGE
            NativeArray<Entity> allSegments = m_AllEdgesQuery.ToEntityArray(Allocator.Temp);
            foreach (Entity entity1 in allSegments)
            {
                if (EntityManager.TryGetComponent(entity1, out Game.Net.Edge edge) && (edge.m_Start == Entity.Null || edge.m_End == Entity.Null))
                {
                    buffer.AddComponent<Deleted>(entity1);
                    m_Log.Info($"{nameof(FindBrokenNodesSystem)}.{nameof(OnGameLoadingComplete)} Deleted edge with no connected edge. Entity {entity1.Index}:{entity1.Version}.");
                }
            }
#endif

            m_Log.Info($"{nameof(FindBrokenNodesSystem)}.{nameof(OnGameLoadingComplete)} Starting. . .");
            if (m_BrokenNodesQuery.IsEmptyIgnoreFilter)
            {
                m_Log.Info($"{nameof(FindBrokenNodesSystem)}.{nameof(OnGameLoadingComplete)} No potentially Broken Nodes detected.");
                return;
            }

            NativeArray<Entity> entities = m_BrokenNodesQuery.ToEntityArray(Allocator.Temp);
            NativeList<Entity> brokenNodes = new NativeList<Entity>(0, Allocator.Temp);

            m_Log.Info($"{nameof(FindBrokenNodesSystem)}.{nameof(OnGameLoadingComplete)} Evaluating Potentially Broken Nodes:");
            foreach (Entity entity in entities)
            {
                if (EntityManager.TryGetComponent(entity, out PrefabRef prefabRef)
                    && m_PrefabSystem.TryGetPrefab(prefabRef.m_Prefab, out PrefabBase prefab)
                    && prefab is not null
                    && ((EntityManager.TryGetComponent(prefabRef.m_Prefab, out Game.Prefabs.ElectricityConnectionData electricityConnectionData)
                    && EvaluateEelctricityConnectionData(electricityConnectionData, entity))
                    || EntityManager.HasComponent<Game.Prefabs.WaterPipeConnectionData>(prefabRef.m_Prefab)))
                {
                    brokenNodes.Add(entity);
                    m_Log.Info($"{nameof(FindBrokenNodesSystem)}.{nameof(OnGameLoadingComplete)} Found Broken Node {entity.Index}:{entity.Version}.");
                    if (m_PrefabSystem.TryGetPrefab(prefabRef.m_Prefab, out PrefabBase prefabBase))
                    {
                        m_Log.Info($"{nameof(FindBrokenNodesSystem)}.{nameof(OnGameLoadingComplete)} Broken Node is a {prefabBase.name}.");
                    }
                }
                else if (EntityManager.TryGetBuffer(entity, isReadOnly: true, out DynamicBuffer<Game.Net.ConnectedEdge> connectedEdges) && !EntityManager.HasComponent<Game.Net.LocalConnect>(entity))
                {
                    foreach (Game.Net.ConnectedEdge edge in connectedEdges)
                    {
                        if (EntityManager.TryGetComponent(edge.m_Edge, out PrefabRef edgePrefabRef)
                            && m_PrefabSystem.TryGetPrefab(edgePrefabRef.m_Prefab, out PrefabBase edgePrefab)
                            && edgePrefab is not null
                            && ((EntityManager.TryGetComponent(edgePrefabRef.m_Prefab, out Game.Prefabs.ElectricityConnectionData edgeElectricityConnectionData)
                            && EvaluateEelctricityConnectionData(edgeElectricityConnectionData, edge.m_Edge))
                            || EntityManager.HasComponent<Game.Prefabs.WaterPipeConnectionData>(edgePrefabRef.m_Prefab)))
                        {
                            brokenNodes.Add(entity);
                            m_Log.Info($"{nameof(FindBrokenNodesSystem)}.{nameof(OnGameLoadingComplete)} Found Broken Node {entity.Index}:{entity.Version}.");
                            if (m_PrefabSystem.TryGetPrefab(prefabRef.m_Prefab, out PrefabBase prefabBase))
                            {
                                m_Log.Info($"{nameof(FindBrokenNodesSystem)}.{nameof(OnGameLoadingComplete)} Broken Edge is a {edgePrefab.name}.");
                            }
                        }
                    }
                }
            }

            if (brokenNodes.Length == 0)
            {
                m_Log.Info($"{nameof(FindBrokenNodesSystem)}.{nameof(OnGameLoadingComplete)} No Broken Nodes detected.");
            }
            else
            {
                m_Log.Info($"{nameof(FindBrokenNodesSystem)}.{nameof(OnGameLoadingComplete)} Highlighting connected edges of broken nodes:");
                foreach (Entity entity in brokenNodes)
                {
                    if (!EntityManager.TryGetBuffer(entity, isReadOnly: true, out DynamicBuffer<Game.Net.ConnectedEdge> connectedEdges))
                    {
                        continue;
                    }

                    foreach (Game.Net.ConnectedEdge edge in connectedEdges)
                    {
                        buffer.AddComponent<Highlighted>(edge.m_Edge);

                        m_Log.Info($"{nameof(FindBrokenNodesSystem)}.{nameof(OnGameLoadingComplete)} Highlighted {edge.m_Edge}.");
                        buffer.AddComponent<BatchesUpdated>(edge.m_Edge);
                    }
                }
            }

            m_Log.Info($"{nameof(FindBrokenNodesSystem)}.{nameof(OnGameLoadingComplete)} Finished.");
        }

        /// <inheritdoc/>
        protected override void OnUpdate()
        {
        }

        private bool EvaluateEelctricityConnectionData(Game.Prefabs.ElectricityConnectionData electricityConnectionData, Entity entity)
        {
            if (electricityConnectionData.m_CompositionAll.m_General == 0 && electricityConnectionData.m_CompositionAll.m_Left == 0 && electricityConnectionData.m_CompositionAll.m_Right == 0)
            {
                return true;
            }

            if (electricityConnectionData.m_CompositionAll.m_General == CompositionFlags.General.Lighting)
            {
                if (!EntityManager.TryGetBuffer(entity, isReadOnly: true, out DynamicBuffer<Game.Net.ConnectedEdge> connectedEdges))
                {
                    return false;
                }

                foreach (Game.Net.ConnectedEdge edge in connectedEdges)
                {
                    if (EntityManager.TryGetComponent(edge.m_Edge, out Game.Net.Upgraded upgraded) && (upgraded.m_Flags.m_General & CompositionFlags.General.Lighting) == CompositionFlags.General.Lighting)
                    {
                        return true;
                    }
                }

                if (EntityManager.TryGetComponent(entity, out Game.Net.Upgraded entityUpgraded) && (entityUpgraded.m_Flags.m_General & CompositionFlags.General.Lighting) == CompositionFlags.General.Lighting)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
