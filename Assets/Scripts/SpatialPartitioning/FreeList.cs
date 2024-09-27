using System.Collections;
using System.Collections.Generic;
using UnityEditor.Profiling.Memory.Experimental;
using UnityEngine;
using static UnityEditor.Progress;

namespace BoidsProject
{
    public class FreeList<T>
    {
        private class FreeListItem
        {
            public T item;
            public int next;

            public FreeListItem(T item, int next)
            {
                this.item = item;
                this.next = next;
            }
        }

        private List<FreeListItem> items;
        private int firstFree;

        public FreeList()
        {
            items = new();
            firstFree = -1;
        }
        public int Add(T item)
        {
            if (firstFree == -1)
            {
                items.Add(new FreeListItem(item, -1));
                return items.Count - 1;
            }
            else
            {
                int index = firstFree;
                firstFree = items[index].next;
                items[index].item = item;
                return index;
            }
        }
        public int GetExisting()
        {
            int index = firstFree;
            firstFree = items[index].next;
            return index;
        }
        public void Remove(int index)
        {
            items[index].next = firstFree;
            firstFree = index;
        }
        public T this[int index]
        {
            get { return items[index].item; }
            set { items[index].item = value; }
        }
        public bool IsFull()
        {
            return firstFree == -1;
        }
        public int Range()
        {
            return items.Count;
        }
    }
}
