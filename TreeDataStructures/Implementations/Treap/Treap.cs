using System.Diagnostics.CodeAnalysis;
using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.Treap;

public class Treap<TKey, TValue> : BinarySearchTreeBase<TKey, TValue, TreapNode<TKey, TValue>>
    where TKey : IComparable<TKey>
{
    /// <summary>
    /// Разрезает дерево с корнем <paramref name="root"/> на два поддерева:
    /// Left: все ключи <= <paramref name="key"/>
    /// Right: все ключи > <paramref name="key"/>
    /// </summary>
    protected virtual (TreapNode<TKey, TValue>? Left, TreapNode<TKey, TValue>? Right) Split(TreapNode<TKey, TValue>? root, TKey key)
    {
        if (root == null) return (null, null);

        int cmp = Comparer.Compare(root.Key, key); 

        if (cmp <= 0)
        {
            var splitResult = Split(root.Right, key);
            root.Right = splitResult.Left;

            if (root.Right != null)
            {
                root.Right.Parent = root;
            }

            if (splitResult.Right != null) 
            {
                splitResult.Right.Parent = null;
            }

            return (root, splitResult.Right);
        }
        else
        {
            var splitResult = Split(root.Left, key);
            root.Left = splitResult.Right;

            if (root.Left != null)
            {
                root.Left.Parent = root; 
            }

            if (splitResult.Left != null)
            {
                splitResult.Left.Parent = null;
            }

            return (splitResult.Left, root); 
        }
    }

    /// <summary>
    /// Сливает два дерева в одно.
    /// Важное условие: все ключи в <paramref name="left"/> должны быть меньше ключей в <paramref name="right"/>.
    /// Слияние происходит на основе Priority (куча).
    /// </summary>
    protected virtual TreapNode<TKey, TValue>? Merge(TreapNode<TKey, TValue>? left, TreapNode<TKey, TValue>? right)
    {
        if (left == null) return right;
        if (right == null) return left;

        if (left.Priority > right.Priority)
        {
            TreapNode<TKey, TValue>? newRight = Merge(left.Right, right);
            left.Right = newRight; 

            if (newRight != null) {
                newRight.Parent = left;
            }

            return left;
        }
        else
        {
            TreapNode<TKey, TValue>? newLeft = Merge(left, right.Left);
            right.Left = newLeft;
            
            if (newLeft != null)
            {
                newLeft.Parent = right;
            }

            return right;
        }
    }
    

    public override void Add(TKey key, TValue value)
    {
        if (key == null) throw new Exception("NULL");

        var existing = (TreapNode<TKey, TValue>?)FindNode(key); 

        if (existing != null) 
        {
            existing.Value = value;
            return;
        }

        var newNode = CreateNode(key, value);

        var (left, right) = Split((TreapNode<TKey, TValue>?)Root, key);
        var tempRoot = Merge(left, newNode);

        Root = Merge(tempRoot, right);

        if (Root != null) Root.Parent = null;

        Count++;
        OnNodeAdded(newNode);
    }

    public override bool Remove(TKey key)
    {
        if (key == null) throw new Exception("NULL");

        var node = (TreapNode<TKey, TValue>?)FindNode(key);
        if (node == null) return false;

        var mergedChildren = Merge(node.Left, node.Right);

        if (node.Parent == null) 
        {
            Root = mergedChildren;
        }
        else
        {
            if (node.Parent.Left == node)
                node.Parent.Left = mergedChildren;
            else
                node.Parent.Right = mergedChildren;
        }

        if (mergedChildren != null) 
        {
            mergedChildren.Parent = node.Parent;
        }

        node.Parent = null;
        node.Left = null;
        node.Right = null;

        Count--;
        OnNodeRemoved(node.Parent, mergedChildren);
        
        return true;
    }

    protected override TreapNode<TKey, TValue> CreateNode(TKey key, TValue value)
    {
        return new TreapNode<TKey, TValue>(key, value);
    }
    protected override void OnNodeAdded(TreapNode<TKey, TValue> newNode)
    {
    }
    
    protected override void OnNodeRemoved(TreapNode<TKey, TValue>? parent, TreapNode<TKey, TValue>? child)
    {
    }
    
}