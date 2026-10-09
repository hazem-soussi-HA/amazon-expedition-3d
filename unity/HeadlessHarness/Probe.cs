using System;
using UnityEngine;

namespace AmazonExpedition.Harness
{
    public static class Probe
    {
        public static void Run()
        {
            RaycastHit hit;
            var start = new Vector3(3.45f, -1.93f, 21.33f);
            var ground = JungleTerrain.MeshHeight(start.x, start.z);
            Console.WriteLine("start {0} ground h {1:0.000} drop {2:0.000}", start, ground, start.y - ground);

            var ok = Physics.Raycast(start, Vector3.down, out hit, 2.2f, ~0, QueryTriggerInteraction.Ignore);
            Console.WriteLine("Physics.Raycast -> {0}  hit={1}", ok, ok ? hit.point.ToString() + " n=" + hit.normal : "-");

            var ok2 = Physics.Raycast(start, Vector3.down, 2.2f, ~0, QueryTriggerInteraction.Ignore);
            Console.WriteLine("Physics.Raycast (no out) -> {0}", ok2);

            RaycastHit raw;
            var ok3 = PhysicsEngine.Ray(start, Vector3.down, 2.2f, ~0, QueryTriggerInteraction.Ignore, out raw);
            Console.WriteLine("PhysicsEngine.Ray -> {0}", ok3);

            Console.WriteLine("UseHeightfield {0}  shapes {1}", PhysicsEngine.UseHeightfield, PhysicsEngine.Shapes.Count);
            Console.WriteLine("mesh cell {0:0.000}", JungleTerrain.MESH_CELL);
        }
    }
}