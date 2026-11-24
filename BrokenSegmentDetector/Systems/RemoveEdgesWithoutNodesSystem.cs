// <copyright file="RemoveEdgesWithoutNodesSystem.cs" company="Yenyang's Mods. MIT License">
// Copyright (c) Yenyang's Mods. MIT License. All rights reserved.
// </copyright>
namespace BrokenSegmentDetector.Systems
{
    using System.Collections.Generic;
    using Colossal.Logging;
    using Game;
    using Game.Prefabs;
    using Unity.Collections;
    using Unity.Entities;

    /// <summary>
    /// Removes edges (segments) whose start or end node is missing while the save is being deserialized.
    /// Runs before <see cref="Game.Serialization.ConnectedEdgeSystem"/>, i.e. before the vanilla
    /// <see cref="Game.Serialization.ElectricityGraphSystem"/> rebuilds the electricity graph. Such edges make
    /// that system crash (null pointer in Burst code) while loading, so the game never reaches
    /// <see cref="FindBrokenNodesSystem"/> unless Burst compilation is disabled.
    /// </summary>
    public partial class RemoveEdgesWithoutNodesSystem : GameSystemBase
    {
        private EntityQuery m_AllEdgesQuery;
        private PrefabSystem m_PrefabSystem;
        private ILog m_Log;

        /// <inheritdoc/>
        protected override void OnCreate()
        {
            base.OnCreate();
            m_Log = Mod.log;
            m_Log.Info($"{nameof(RemoveEdgesWithoutNodesSystem)}.{nameof(OnCreate)}");
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_AllEdgesQuery = GetEntityQuery(ComponentType.ReadOnly<Game.Net.Edge>());
            RequireForUpdate(m_AllEdgesQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate()
        {
            NativeArray<Entity> allSegments = m_AllEdgesQuery.ToEntityArray(Allocator.Temp);
            HashSet<Entity> entitiesToDestroy = new HashSet<Entity>();
            int brokenEdges = 0;
            m_Log.Info($"{nameof(RemoveEdgesWithoutNodesSystem)}.{nameof(OnUpdate)} Checking {allSegments.Length} edges for missing start or end nodes.");

            foreach (Entity entity in allSegments)
            {
                Game.Net.Edge edge = EntityManager.GetComponentData<Game.Net.Edge>(entity);
                if (IsValidNode(edge.m_Start) && IsValidNode(edge.m_End))
                {
                    continue;
                }

                brokenEdges++;
                m_Log.Info($"{nameof(RemoveEdgesWithoutNodesSystem)}.{nameof(OnUpdate)} Removing edge {entity.Index}:{entity.Version} ({GetPrefabName(entity)}). Start node {edge.m_Start.Index}:{edge.m_Start.Version}, end node {edge.m_End.Index}:{edge.m_End.Version}.");
                CollectEdgeForRemoval(entity, entitiesToDestroy);
            }

            allSegments.Dispose();

            if (entitiesToDestroy.Count == 0)
            {
                m_Log.Info($"{nameof(RemoveEdgesWithoutNodesSystem)}.{nameof(OnUpdate)} No edges with missing nodes detected.");
                return;
            }

            NativeArray<Entity> destroy = new NativeArray<Entity>(entitiesToDestroy.Count, Allocator.Temp);
            int i = 0;
            foreach (Entity entity in entitiesToDestroy)
            {
                destroy[i++] = entity;
            }

            EntityManager.DestroyEntity(destroy);
            destroy.Dispose();
            m_Log.Info($"{nameof(RemoveEdgesWithoutNodesSystem)}.{nameof(OnUpdate)} Removed {brokenEdges} edge(s) with missing nodes ({entitiesToDestroy.Count} entities in total).");
        }

        private bool IsValidNode(Entity node)
        {
            return node != Entity.Null && EntityManager.Exists(node) && EntityManager.HasBuffer<Game.Net.ConnectedEdge>(node);
        }

        private string GetPrefabName(Entity entity)
        {
            if (EntityManager.HasComponent<PrefabRef>(entity)
                && m_PrefabSystem.TryGetPrefab(EntityManager.GetComponentData<PrefabRef>(entity), out PrefabBase prefab)
                && prefab is not null)
            {
                return prefab.name;
            }

            return "unknown prefab";
        }

        /// <summary>
        /// Collects the edge and the entities that only exist because of it: sub-lanes, sub-objects and its
        /// electricity / water flow nodes (including their flow edges). Also removes the edge from its aggregate.
        /// </summary>
        private void CollectEdgeForRemoval(Entity edge, HashSet<Entity> entitiesToDestroy)
        {
            entitiesToDestroy.Add(edge);

            if (EntityManager.HasBuffer<Game.Net.SubLane>(edge))
            {
                DynamicBuffer<Game.Net.SubLane> subLanes = EntityManager.GetBuffer<Game.Net.SubLane>(edge, isReadOnly: true);
                foreach (Game.Net.SubLane subLane in subLanes)
                {
                    if (EntityManager.Exists(subLane.m_SubLane))
                    {
                        entitiesToDestroy.Add(subLane.m_SubLane);
                    }
                }
            }

            if (EntityManager.HasBuffer<Game.Objects.SubObject>(edge))
            {
                DynamicBuffer<Game.Objects.SubObject> subObjects = EntityManager.GetBuffer<Game.Objects.SubObject>(edge, isReadOnly: true);
                foreach (Game.Objects.SubObject subObject in subObjects)
                {
                    if (EntityManager.Exists(subObject.m_SubObject))
                    {
                        entitiesToDestroy.Add(subObject.m_SubObject);
                    }
                }
            }

            if (EntityManager.HasComponent<Game.Simulation.ElectricityNodeConnection>(edge))
            {
                CollectFlowNodeForRemoval(EntityManager.GetComponentData<Game.Simulation.ElectricityNodeConnection>(edge).m_ElectricityNode, entitiesToDestroy);
            }

            if (EntityManager.HasComponent<Game.Simulation.WaterPipeNodeConnection>(edge))
            {
                CollectFlowNodeForRemoval(EntityManager.GetComponentData<Game.Simulation.WaterPipeNodeConnection>(edge).m_WaterPipeNode, entitiesToDestroy);
            }

            if (EntityManager.HasComponent<Game.Net.Aggregated>(edge))
            {
                Entity aggregate = EntityManager.GetComponentData<Game.Net.Aggregated>(edge).m_Aggregate;
                if (EntityManager.Exists(aggregate) && EntityManager.HasBuffer<Game.Net.AggregateElement>(aggregate))
                {
                    DynamicBuffer<Game.Net.AggregateElement> elements = EntityManager.GetBuffer<Game.Net.AggregateElement>(aggregate);
                    for (int i = elements.Length - 1; i >= 0; i--)
                    {
                        if (elements[i].m_Edge == edge)
                        {
                            elements.RemoveAt(i);
                        }
                    }
                }
            }
        }

        private void CollectFlowNodeForRemoval(Entity flowNode, HashSet<Entity> entitiesToDestroy)
        {
            if (!EntityManager.Exists(flowNode))
            {
                return;
            }

            entitiesToDestroy.Add(flowNode);
            if (!EntityManager.HasBuffer<Game.Simulation.ConnectedFlowEdge>(flowNode))
            {
                return;
            }

            List<Entity> flowEdges = new List<Entity>();
            foreach (Game.Simulation.ConnectedFlowEdge connectedFlowEdge in EntityManager.GetBuffer<Game.Simulation.ConnectedFlowEdge>(flowNode, isReadOnly: true))
            {
                flowEdges.Add(connectedFlowEdge.m_Edge);
            }

            foreach (Entity flowEdge in flowEdges)
            {
                if (!EntityManager.Exists(flowEdge))
                {
                    continue;
                }

                entitiesToDestroy.Add(flowEdge);
                Entity start = Entity.Null;
                Entity end = Entity.Null;
                if (EntityManager.HasComponent<Game.Simulation.ElectricityFlowEdge>(flowEdge))
                {
                    Game.Simulation.ElectricityFlowEdge electricityFlowEdge = EntityManager.GetComponentData<Game.Simulation.ElectricityFlowEdge>(flowEdge);
                    start = electricityFlowEdge.m_Start;
                    end = electricityFlowEdge.m_End;
                }
                else if (EntityManager.HasComponent<Game.Simulation.WaterPipeEdge>(flowEdge))
                {
                    Game.Simulation.WaterPipeEdge waterPipeEdge = EntityManager.GetComponentData<Game.Simulation.WaterPipeEdge>(flowEdge);
                    start = waterPipeEdge.m_Start;
                    end = waterPipeEdge.m_End;
                }

                // Remove the flow edge from the buffer of the flow node on the other side.
                Entity otherFlowNode = start == flowNode ? end : start;
                if (otherFlowNode != Entity.Null && EntityManager.Exists(otherFlowNode) && EntityManager.HasBuffer<Game.Simulation.ConnectedFlowEdge>(otherFlowNode))
                {
                    DynamicBuffer<Game.Simulation.ConnectedFlowEdge> otherFlowEdges = EntityManager.GetBuffer<Game.Simulation.ConnectedFlowEdge>(otherFlowNode);
                    for (int i = otherFlowEdges.Length - 1; i >= 0; i--)
                    {
                        if (otherFlowEdges[i].m_Edge == flowEdge)
                        {
                            otherFlowEdges.RemoveAt(i);
                        }
                    }
                }
            }
        }
    }
}
