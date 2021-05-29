using System;
using System.Collections.Generic;
using UnityEngine;

namespace upc
{
    // ref: https://github.com/Nition/UnityOctree

    public class Octree<T>
    {
        public int Count { get; private set; }

        private OctreeNode<T> rootNode;
        private readonly float initialSize;
        private readonly float minSize;

        public Octree(float initialWorldSize, Vector3 initialWorldPos, float minNodeSize)
        {
            if (minNodeSize > initialWorldSize)
            {
                throw new ArgumentException($"Minimum node size must be at least as big as the initial world size. {nameof(minNodeSize)}: {minNodeSize}, {nameof(initialWorldSize)}: {initialWorldSize}");
            }
            Count = 0;
            initialSize = initialWorldSize;
            minSize = minNodeSize;
            rootNode = new OctreeNode<T>(initialSize, minSize, initialWorldPos);
        }

        public void Add(T obj, Vector3 objPos)
        {
            // Add object or expand the octree until it can be added
            int safetyCounter = 0; // Safety check against infinite/excessive growth
            while (!rootNode.Add(obj, objPos))
            {
                Grow(objPos - rootNode.Center);
                if (++safetyCounter > 20)
                {
                    throw new ApplicationException("Aborted Add operation as it seemed to be going on forever attempts at growing the octree.");
                }
            }
            Count++;
        }

        public List<T> GetNearBy(Vector3 pos, float maxDistance)
        {
            var results = new List<T>();
            rootNode.GetNearBy(pos, maxDistance * maxDistance, results);
            return results;
        }

        /// <summary>
        /// Draws node boundaries visually for debugging.
        /// Must be called from OnDrawGizmos externally. See also: DrawAllObjects.
        /// </summary>
        public void DrawAllBounds()
        {
            rootNode.DrawAllBounds();
        }

        /// <summary>
        /// Draws the bounds of all objects in the tree visually for debugging.
        /// Must be called from OnDrawGizmos externally. See also: DrawAllBounds.
        /// </summary>
        public void DrawAllObjects()
        {
            rootNode.DrawAllObjects();
        }

        private void Grow(Vector3 direction)
        {
            var xDirection = direction.x >= 0 ? 1 : -1;
            var yDirection = direction.y >= 0 ? 1 : -1;
            var zDirection = direction.z >= 0 ? 1 : -1;
            var oldRoot = rootNode;
            var half = rootNode.SideLength / 2;
            Vector3 newCenter = rootNode.Center + new Vector3(xDirection * half, yDirection * half, zDirection * half);

            // Create a new, bigger octree root node
            rootNode = new OctreeNode<T>(oldRoot, minSize, newCenter);
        }
    }

    public class OctreeNode<T>
    {
        public Vector3 Center { get; private set; }
        public float SideLength { get; private set; }
        public int Count { get; private set; } // 하위 요소들 포함

        private float minSize;
        private Bounds bounds;

        private class Element
        {
            public T Obj;
            public Vector3 Pos;
        }

        //private Dictionary<T, Vector3> objects = new Dictionary<T, Vector3>();
        private readonly List<Element> objects = new List<Element>();

        private OctreeNode<T>[] children;
        private bool HasChildren { get { return children != null; } }
        //private Bounds[] childBounds;

        // If there are already NUM_OBJECTS_ALLOWED in a node, we split it into children
        // A generally good number seems to be something around 8-15
        private const int NUM_OBJECTS_ALLOWED = 8;

        // to revert the bounds size after temporary changes
        private Vector3 actualBoundsSize;

        public OctreeNode(float baseLengthVal, float minSizeVal, Vector3 centerVal)
        {
            SetValues(baseLengthVal, minSizeVal, centerVal);
        }

        public OctreeNode(OctreeNode<T> oldRoot, float minSize, Vector3 center)
        {
            var newLength = oldRoot.SideLength * 2;
            SetValues(newLength, minSize, center);
            if (oldRoot.Empty()) return;

            int oldRootPosIndex = FindChildIndex(oldRoot.Center);
            CreateChildren();
            children[oldRootPosIndex] = oldRoot;
        }

        public bool Add(T obj, Vector3 objPos)
        {
            if (bounds.Contains(objPos) == false)
            {
                return false;
            }
            Add(new Element { Obj = obj, Pos = objPos });
            return true;
        }

        public void GetNearBy(Vector3 pos, float sqrDistance, List<T> result)
        {
            //var srcBounds = new Bounds(pos, Vector3.one * sqrDistance);
            //if (bounds.Intersects(srcBounds) == false) return; // not interested
            if ((bounds.ClosestPoint(pos) - pos).sqrMagnitude > sqrDistance) return; // not interested
            if (Empty()) return;

            foreach (var elem in objects)
            {
                if ((pos - elem.Pos).sqrMagnitude < sqrDistance)
                {
                    result.Add(elem.Obj);
                }
            }

            foreach (var child in children)
            {
                child.GetNearBy(pos, sqrDistance, result);
            }
        }

        /// <summary>
        /// Draws node boundaries visually for debugging.
        /// Must be called from OnDrawGizmos externally. See also: DrawAllObjects.
        /// </summary>
        /// <param name="depth">Used for recurcive calls to this method.</param>
        public void DrawAllBounds(float depth = 0)
        {
            float tintVal = depth / 10; // Will eventually get values > 1. Color rounds to 1 automatically
            Gizmos.color = new Color(tintVal, 0, 1.0f - tintVal);

            Bounds thisBounds = new Bounds(Center, new Vector3(SideLength, SideLength, SideLength));
            Gizmos.DrawWireCube(thisBounds.center, thisBounds.size);

            if (children != null)
            {
                depth++;
                foreach (var child in children) child.DrawAllBounds(depth);
            }
            Gizmos.color = Color.white;
        }

        /// <summary>
        /// Draws the bounds of all objects in the tree visually for debugging.
        /// Must be called from OnDrawGizmos externally. See also: DrawAllBounds.
        /// NOTE: marker.tif must be placed in your Unity /Assets/Gizmos subfolder for this to work.
        /// </summary>
        public void DrawAllObjects()
        {
            float tintVal = SideLength / 20;
            Gizmos.color = new Color(0, 1.0f - tintVal, tintVal, 0.25f);

            foreach (var elem in objects)
            {
                Gizmos.DrawIcon(elem.Pos, "animationkeyframe", false);
            }

            if (children != null)
            {
                foreach (var child in children) child.DrawAllObjects();
            }

            Gizmos.color = Color.white;
        }

        private bool Empty()
        {
            if (objects.Count > 0) return true;
            if (children == null) return false;
            foreach (var child in children)
                if (child.Empty() == false) return true;
            return false;
        }

        private void SetValues(float baseLengthVal, float minSizeVal, Vector3 centerVal)
        {
            SideLength = baseLengthVal;
            minSize = minSizeVal;
            Center = centerVal;

            // Create the bounding box.
            actualBoundsSize = new Vector3(SideLength, SideLength, SideLength);
            bounds = new Bounds(Center, actualBoundsSize);
        }

        /// <summary>
        ///     add a element in given position
        /// </summary>
        /// <param name="e">elemt to be added</param>
        /// <returns>a leaf node which contains the point</returns>
        private OctreeNode<T> Add(Element e)
        {
            // We know it fits at this level if we've got this far
            ++Count; // never fails

            // We always put things in the deepest possible child
            // So we can skip checks and simply move down if there are children aleady
            if (HasChildren == false)
            {
                // Just add if few objects are here, or children would be below min size
                if (objects.Count < NUM_OBJECTS_ALLOWED || (SideLength / 2) < minSize)
                {
                    objects.Add(e);
                    return this; // We're done. No children yet
                }

                // Enough objects in this node already: Create the 8 children
                CreateChildren();
                // Now that we have the new children, move this node's existing objects into them
                foreach (var elem in objects)
                {
                    FindChild(elem.Pos).Add(elem);
                }
                objects.Clear(); // Remove from here
            }

            // Handle the new object we're adding now
            return FindChild(e.Pos).Add(e);
        }

        private void CreateChildren()
        {
            float quarter = SideLength / 4f;
            float newLength = SideLength / 2;
            children = new OctreeNode<T>[8];
            children[0] = new OctreeNode<T>(newLength, minSize, Center + new Vector3(-quarter, quarter, -quarter));
            children[1] = new OctreeNode<T>(newLength, minSize, Center + new Vector3(quarter, quarter, -quarter));
            children[2] = new OctreeNode<T>(newLength, minSize, Center + new Vector3(-quarter, quarter, quarter));
            children[3] = new OctreeNode<T>(newLength, minSize, Center + new Vector3(quarter, quarter, quarter));
            children[4] = new OctreeNode<T>(newLength, minSize, Center + new Vector3(-quarter, -quarter, -quarter));
            children[5] = new OctreeNode<T>(newLength, minSize, Center + new Vector3(quarter, -quarter, -quarter));
            children[6] = new OctreeNode<T>(newLength, minSize, Center + new Vector3(-quarter, -quarter, quarter));
            children[7] = new OctreeNode<T>(newLength, minSize, Center + new Vector3(quarter, -quarter, quarter));
        }

        private OctreeNode<T> FindChild(Vector3 objPos)
        {
            // Find which child the object is closest to based on where the
            // object's center is located in relation to the octree's center
            var index = FindChildIndex(objPos);
            return children[index];
        }

        private int FindChildIndex(Vector3 objPos)
        {
            return (objPos.x <= Center.x ? 0 : 1) + (objPos.y >= Center.y ? 0 : 4) + (objPos.z <= Center.z ? 0 : 2);
        }
    }
}