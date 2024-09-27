using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace BoidsProject
{
    public class Octree<T>
    {
        private class OctNode
        {
            public int child;
            public int count;
            public Bounds bounds;

            public bool IsLeaf()
            {
                return count != -1;
            }

            public OctNode()
            {
                child = -1;
                count = 0;
                bounds = new Bounds();
            }
        }
        private class OctElement
        {
            public T element;
            public Bounds bounds;
            public int next;

            public OctElement(T element, Bounds bounds, int next)
            {
                this.element = element;
                this.bounds = bounds;
                this.next = next;
            }
        }

        private int[] rootExtents;

        private int capacity;
        private int maxDepth;

        private FreeList<OctNode> nodes;
        private FreeList<OctElement> elements;

        private EqualityComparer<T> compare;

        private Stack<Stack<int>> toResize;

        public Octree(Bounds bounds, int capacity, int maxDepth)
        {
            rootExtents = new int[6];

            nodes = new FreeList<OctNode>();
            elements = new FreeList<OctElement>();

            compare = EqualityComparer<T>.Default;

            toResize = new();

            rootExtents[0] = (int)bounds.min.x;
            rootExtents[1] = (int)bounds.max.x;
            rootExtents[2] = (int)bounds.min.y;
            rootExtents[3] = (int)bounds.max.y;
            rootExtents[4] = (int)bounds.min.z;
            rootExtents[5] = (int)bounds.max.z;

            nodes.Add(new OctNode());

            this.capacity = capacity;
            this.maxDepth = maxDepth;
        }
        public void Insert(T element, Bounds bounds)
        {
            Assert.IsTrue(IsIn(bounds.center, rootExtents), "Error: Invalid insertion into tree, item \"" + element.ToString() + "\" at " + bounds.center + " is not within the bounds of the tree.");


            int current = 0;
            Span<int> extents = stackalloc int[6];
            int depth = 1;

            for (int i = 0; i < rootExtents.Length; i++)
            {
                extents[i] = rootExtents[i];
            }

            while (depth <= maxDepth)
            {
                if (!nodes[current].bounds.Contains(bounds.max) || !nodes[current].bounds.Contains(bounds.min))
                    Union(current, bounds);

                if (nodes[current].IsLeaf())
                {
                    if (elements.IsFull())
                        nodes[current].child = elements.Add(new OctElement(element, bounds, nodes[current].child));
                    else
                    {
                        int index = elements.GetExisting();

                        elements[index].element = element;
                        elements[index].bounds = bounds;
                        elements[index].next = nodes[current].child;

                        nodes[current].child = index;
                    }
                    nodes[current].count++;
                    if (capacity < nodes[current].count && depth < maxDepth)
                        Divide(current, extents, depth);

                    break;
                }
                current = FindContainingChild(current, extents, bounds.center);
                depth++;
            }
        }
        public void Remove(T element, Bounds bounds)
        {
            Assert.IsTrue(IsIn(bounds.center, rootExtents), "Error: Invalid removal from tree, item \"" + element.ToString() + "\" at " + bounds.center + " is not within the bounds of the tree.");

            int current = 0;
            Span<int> extents = stackalloc int[6];

            Stack<int> path = new();
            path.Push(0);

            for (int i = 0; i < rootExtents.Length; i++)
            {
                extents[i] = rootExtents[i];
            }

            while (path.Count <= maxDepth)
            {
                if (nodes[current].IsLeaf())
                {
                    bool resizingRequired = nodes[current].bounds.max.x >= bounds.max.x ||
                        nodes[current].bounds.min.x <= bounds.min.x ||
                        nodes[current].bounds.max.y >= bounds.max.y ||
                        nodes[current].bounds.min.y <= bounds.min.y ||
                        nodes[current].bounds.max.z >= bounds.max.z ||
                        nodes[current].bounds.min.z <= bounds.min.z;

                    int previousItem = -1;
                    int currentItem = nodes[current].child;

                    while (!compare.Equals(elements[currentItem].element, element))
                    {
                        previousItem = currentItem;
                        currentItem = elements[currentItem].next;
                    }
                    if (previousItem == -1)
                        nodes[current].child = elements[currentItem].next;
                    else
                        elements[previousItem].next = elements[currentItem].next;

                    elements.Remove(currentItem);
                    nodes[current].count--;

                    if (resizingRequired)
                    {
                        toResize.Push(path);
                    }
                    return;
                }

                current = FindContainingChild(current, extents, bounds.center);
                path.Push(current);
            }
        }
        public List<T> Query(Bounds area)
        {
            List<T> hits = new();
            Stack<int> toProcess = new();
            toProcess.Push(0);

            while (toProcess.Count > 0)
            {
                var current = toProcess.Pop();

                if (nodes[current].IsLeaf())
                {
                    int index = nodes[current].child;
                    while (index != -1)
                    {
                        if (area.Intersects(elements[index].bounds))
                            hits.Add(elements[index].element);
                        index = elements[index].next;
                    }
                    continue;
                }

                int o1 = nodes[current].child;

                for (int i = 0; i < 8; i++)
                {
                    if (nodes[o1 + i].bounds.Intersects(area))
                    {
                        toProcess.Push(o1 + i);
                    }
                }
            }
            return hits;
        }
        public void Update()
        {
            if (nodes[0].IsLeaf())
                return;

            Stack<int> toProcess = new();
            toProcess.Push(0);

            while (toProcess.Count > 0)
            {
                int current = toProcess.Pop();

                int o1 = nodes[current].child;

                if (nodes[o1].count == 0 &&
                    nodes[o1 + 1].count == 0 &&
                    nodes[o1 + 2].count == 0 &&
                    nodes[o1 + 3].count == 0 &&
                    nodes[o1 + 4].count == 0 &&
                    nodes[o1 + 5].count == 0 &&
                    nodes[o1 + 6].count == 0 &&
                    nodes[o1 + 7].count == 0)
                {
                    for (int i = 7; i >= 0; i--)
                    {
                        nodes.Remove(o1 + i);
                    }
                    nodes[current].count = 0;
                    nodes[current].child = -1;

                    nodes[current].bounds.min = Vector3.zero;
                    nodes[current].bounds.max = Vector3.zero;

                    continue;
                }

                for (int i = 0; i < 8; i++)
                {
                    if (!nodes[o1 + i].IsLeaf())
                    {
                        toProcess.Push(o1 + i);
                    }
                }
            }
            while (toResize.Count > 0)
            {
                Stack<int> path = toResize.Pop();
                while (path.Count > 0)
                {
                    int current = path.Pop();

                    nodes[current].bounds.min = Vector3.zero;
                    nodes[current].bounds.max = Vector3.zero;

                    int child = nodes[current].child;

                    if (nodes[current].IsLeaf())
                    {
                        while (child != -1)
                        {
                            Union(current, elements[child].bounds);
                            child = elements[child].next;
                        }
                    }
                    else
                    {
                        for (int i = 0; i < 8; i++)
                        {
                            Union(current, nodes[child + i].bounds);
                        }
                    }
                }
            }
        }
        private void Divide(int leaf, Span<int> extents, int depth)
        {
            int itemIndex = nodes[leaf].child;

            nodes[leaf].count = -1;

            int o1;

            if (nodes.IsFull())
            {
                o1 = nodes.Add(new OctNode());
                nodes.Add(new OctNode());
                nodes.Add(new OctNode());
                nodes.Add(new OctNode());
                nodes.Add(new OctNode());
                nodes.Add(new OctNode());
                nodes.Add(new OctNode());
                nodes.Add(new OctNode());
            }
            else
            {
                o1 = nodes.GetExisting();
                nodes[o1].count = 0;
                nodes[o1].child = -1;
                nodes[o1].bounds.min = Vector3.zero;
                nodes[o1].bounds.max = Vector3.zero;


                for (int i = 1; i < 8; i++)
                {
                    nodes.GetExisting();
                    nodes[o1 + i].count = 0;
                    nodes[o1 + i].child = -1;
                    nodes[o1 + i].bounds.min = Vector3.zero;
                    nodes[o1 + i].bounds.max = Vector3.zero;
                }
            }

            nodes[leaf].child = o1;

            while (itemIndex != -1)
            {
                int next = elements[itemIndex].next;

                Span<int> childExtents = stackalloc int[6];
                for(int i = 0; i < 6; i++)
                    childExtents[i] = extents[i];

                int o = FindContainingChild(leaf, childExtents, elements[itemIndex].bounds.center);

                elements[itemIndex].next = nodes[o].child;
                nodes[o].child = itemIndex;
                nodes[o].count++;
                Union(o, elements[itemIndex].bounds);

                itemIndex = next;
            }

            int w = extents[1] - extents[0];
            int h = extents[3] - extents[2];
            int d = extents[5] - extents[4];

            int x1 = extents[0];
            int x2 = extents[1];
            int y1 = extents[2];
            int y2 = extents[3];
            int z1 = extents[4];
            int z2 = extents[5];

            for (int i = 0; i < 8; i++)
            {
                extents[0] = (i & 1) == 0 ? x1 : x1 + w / 2;
                extents[1] = (i & 1) == 0 ? x1 + w / 2 : x2;
                extents[2] = ((i / 2) & 1) == 0 ? y1 : y1 + h / 2;
                extents[3] = ((i / 2) & 1) == 0 ? y1 + h / 2 : y2;
                extents[4] = ((i / 4) & 1) == 0 ? z1 : z1 + d / 2;
                extents[5] = ((i / 4) & 1) == 0 ? z1 + d / 2 : z2;

                if (capacity < nodes[o1 + i].count && depth + 1 < maxDepth)
                    Divide(o1 + i, extents, depth + 1);
            }
        }
        private int FindContainingChild(int node, Span<int> extents, Vector3 point)
        {
            int o1 = nodes[node].child;

            int w = extents[1] - extents[0];
            int h = extents[3] - extents[2];
            int d = extents[5] - extents[4];

            int x1 = extents[0];
            int x2 = extents[1];
            int y1 = extents[2];
            int y2 = extents[3];
            int z1 = extents[4];
            int z2 = extents[5];


            for (int i = 0; i < 8; i++)
            {
                extents[0] = (i & 1) == 0 ? x1 : x1 + w / 2;
                extents[1] = (i & 1) == 0 ? x1 + w / 2 : x2;
                extents[2] = ((i / 2) & 1) == 0 ? y1 : y1 + h / 2;
                extents[3] = ((i / 2) & 1) == 0 ? y1 + h / 2 : y2;
                extents[4] = ((i / 4) & 1) == 0 ? z1 : z1 + d / 2;
                extents[5] = ((i / 4) & 1) == 0 ? z1 + d / 2 : z2;

                if (IsIn(point, extents))
                    return o1 + i;
            }
            return -1;
        }
        private void Union(int node, Bounds bounds2)
        {
            if(nodes[node].bounds.extents == Vector3.zero)
            {
                nodes[node].bounds = bounds2;
            }

            nodes[node].bounds.min = new Vector3(Mathf.Min(nodes[node].bounds.min.x, bounds2.min.x),
                          Mathf.Min(nodes[node].bounds.min.y, bounds2.min.y),
                          Mathf.Min(nodes[node].bounds.min.z, bounds2.min.z));
            nodes[node].bounds.max = new Vector3(Mathf.Max(nodes[node].bounds.max.x, bounds2.max.x),
                          Mathf.Max(nodes[node].bounds.max.y, bounds2.max.y),
                          Mathf.Max(nodes[node].bounds.max.z, bounds2.max.z));
        }
        private bool IsIn(Vector3 point, Span<int> bounds)
        {
            return bounds[0] <= point.x &&
                   point.x <= bounds[1] &&
                   bounds[2] <= point.y &&
                   point.y <= bounds[3] &&
                   bounds[4] <= point.z &&
                   point.z <= bounds[5];

        }
        private bool IsIn(Vector3 point, int[] bounds)
        {
            return bounds[0] <= point.x &&
                   point.x <= bounds[1] &&
                   bounds[2] <= point.y &&
                   point.y <= bounds[3] &&
                   bounds[4] <= point.z &&
                   point.z <= bounds[5];

        }
        public void Draw()
        {
            Stack<OctNode> toProcess = new();
            toProcess.Push(nodes[0]);

            while (toProcess.Count > 0)
            {
                var current = toProcess.Pop();
                DrawBounds(current.bounds, Color.red);

                if (current.IsLeaf())
                {
                    int index = current.child;
                    while (index != -1)
                    {
                        DrawBounds(elements[index].bounds, Color.blue);
                        index = elements[index].next;
                    }
                }
                else
                {
                    int o1 = current.child;

                    for (int i = 0; i < 8; i++)
                    {
                        toProcess.Push(nodes[o1 + i]);
                    }
                }
            }
        }
        private void DrawBounds(Bounds b, Color color, float delay = 0)
        {
            // bottom
            var p1 = new Vector3(b.min.x, b.min.y, b.min.z);
            var p2 = new Vector3(b.max.x, b.min.y, b.min.z);
            var p3 = new Vector3(b.max.x, b.min.y, b.max.z);
            var p4 = new Vector3(b.min.x, b.min.y, b.max.z);

            Debug.DrawLine(p1, p2, color, delay);
            Debug.DrawLine(p2, p3, color, delay);
            Debug.DrawLine(p3, p4, color, delay);
            Debug.DrawLine(p4, p1, color, delay);

            // top
            var p5 = new Vector3(b.min.x, b.max.y, b.min.z);
            var p6 = new Vector3(b.max.x, b.max.y, b.min.z);
            var p7 = new Vector3(b.max.x, b.max.y, b.max.z);
            var p8 = new Vector3(b.min.x, b.max.y, b.max.z);

            Debug.DrawLine(p5, p6, color, delay);
            Debug.DrawLine(p6, p7, color, delay);
            Debug.DrawLine(p7, p8, color, delay);
            Debug.DrawLine(p8, p5, color, delay);

            // sides
            Debug.DrawLine(p1, p5, color, delay);
            Debug.DrawLine(p2, p6, color, delay);
            Debug.DrawLine(p3, p7, color, delay);
            Debug.DrawLine(p4, p8, color, delay);
        }
    }
}
