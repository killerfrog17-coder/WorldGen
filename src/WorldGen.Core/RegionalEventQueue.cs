using System;
using System.Collections.Generic;
namespace WorldGen.Core
{
    // Persistent leftist heap: an event changes O(log E) nodes. Older UI/save
    // snapshots share the unchanged branches; publishing never copies a queue.
    [Serializable] public sealed class RegionalQueueNode
    {
        public SimulationEvent Event;
        public RegionalQueueNode Left,Right;
        public int Rank,Count;
        static int Height(RegionalQueueNode n)=>n?.Rank??0;
        public static RegionalQueueNode Merge(RegionalQueueNode a,RegionalQueueNode b)
        {
            if(a==null)return b;if(b==null)return a;if(SimulationEventQueue.Compare(a.Event,b.Event)>0){var t=a;a=b;b=t;}
            var left=a.Left;var right=Merge(a.Right,b);if(Height(left)<Height(right)){var t=left;left=right;right=t;}
            return new RegionalQueueNode{Event=a.Event,Left=left,Right=right,Rank=Height(right)+1,Count=1+(left?.Count??0)+(right?.Count??0)};
        }
        public static RegionalQueueNode Push(RegionalQueueNode root,SimulationEvent item)=>Merge(root,new RegionalQueueNode{Event=item,Rank=1,Count=1});
        public static IEnumerable<SimulationEvent> Entries(RegionalQueueNode root)
        {
            var stack=new Stack<RegionalQueueNode>();if(root!=null)stack.Push(root);
            while(stack.Count>0){var node=stack.Pop();yield return node.Event;if(node.Left!=null)stack.Push(node.Left);if(node.Right!=null)stack.Push(node.Right);}
        }
    }
}
