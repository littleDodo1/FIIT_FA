using System.Collections;
using System.Diagnostics.CodeAnalysis;
using TreeDataStructures.Interfaces;

namespace TreeDataStructures.Core;

public abstract class BinarySearchTreeBase<TKey, TValue, TNode>(IComparer<TKey>? comparer = null) 
    : ITree<TKey, TValue>
    where TNode : Node<TKey, TValue, TNode>
{
    protected TNode? Root;
    public IComparer<TKey> Comparer { get; protected set; } = comparer ?? Comparer<TKey>.Default; // use it to compare Keys

    public int Count { get; protected set; }
    
    public bool IsReadOnly => false;

    public ICollection<TKey> Keys => InOrder().Select(e => e.Key).ToList();
    public ICollection<TValue> Values => InOrder().Select(e => e.Value).ToList();
    
    
    public virtual void Add(TKey key, TValue value)
    {
        if (Root == null)
        {
            Root = CreateNode(key, value);
            Root.Parent = null;
            Count = 1;
            OnNodeAdded(Root);
            return;
        }

        TNode? current = Root;
        TNode? parent = null;

        while (current != null)
        {
            parent = current;
            int cmp = Comparer.Compare(key, current.Key);

            if (cmp == 0)
            {
                current.Value = value;
                return;
            }

            current = cmp < 0 ? current.Left : current.Right;
        }

        TNode newNode = CreateNode(key, value);
        newNode.Parent = parent;

        if (Comparer.Compare(key, parent!.Key) < 0)
            parent.Left = newNode;
        else
            parent.Right = newNode;

        Count++;
        OnNodeAdded(newNode);
    }

    public virtual bool Remove(TKey key)
    {
        TNode? node = FindNode(key);
        if (node == null) { return false; }

        RemoveNode(node);
        this.Count--;
        return true;
    }
    
    
    protected virtual void RemoveNode(TNode node)
    {
        if (node.Left == null)
        {
            Transplant(node, node.Right);
            OnNodeRemoved(node.Parent, node.Right);
            return;
        }

        if (node.Right == null)
        {
            Transplant(node, node.Left);
            OnNodeRemoved(node.Parent, node.Left);
            return;
        }

        TNode successor = Minimum(node.Right);

        if (successor.Parent != node)
        {
            Transplant(successor, successor.Right);
            successor.Right = node.Right;
            successor.Right!.Parent = successor;
        }

        Transplant(node, successor);
        successor.Left = node.Left;
        successor.Left!.Parent = successor;

        OnNodeRemoved(node.Parent, successor);
    }

    public virtual bool ContainsKey(TKey key) => FindNode(key) != null;
    
    public virtual bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        TNode? node = FindNode(key);
        if (node != null)
        {
            value = node.Value;
            return true;
        }
        value = default;
        return false;
    }

    public TValue this[TKey key]
    {
        get => TryGetValue(key, out TValue? val) ? val : throw new KeyNotFoundException();
        set => Add(key, value);
    }

    
    #region Hooks
    
    /// <summary>
    /// Вызывается после успешной вставки
    /// </summary>
    /// <param name="newNode">Узел, который встал на место</param>
    protected virtual void OnNodeAdded(TNode newNode) { }
    
    /// <summary>
    /// Вызывается после удаления. 
    /// </summary>
    /// <param name="parent">Узел, чей ребенок изменился</param>
    /// <param name="child">Узел, который встал на место удаленного</param>
    protected virtual void OnNodeRemoved(TNode? parent, TNode? child) { }
    
    #endregion
    
    
    #region Helpers
    protected abstract TNode CreateNode(TKey key, TValue value);
    
    
    protected TNode? FindNode(TKey key)
    {
        TNode? current = Root;
        while (current != null)
        {
            int cmp = Comparer.Compare(key, current.Key);
            if (cmp == 0) { return current; }
            current = cmp < 0 ? current.Left : current.Right;
        }
        return null;
    }

    private static TNode Minimum(TNode node)
    {
        while (node.Left != null)
            node = node.Left;
        return node;
    }

protected void UpdateDepth(TNode? node)
    {
        while (node != null)
        {
            int leftD  = node.Left?.Depth  ?? 0;
            int rightD = node.Right?.Depth ?? 0;
            int newD   = 1 + Math.Max(leftD, rightD);

            if (newD == node.Depth) break;

            node.Depth = newD;
            node = node.Parent;
        }
    }

    protected void RotateLeft(TNode x)
    {
        if (x == null || x.Right == null) return;

        TNode y = x.Right;

        x.Right = y.Left;
        if (y.Left != null) y.Left.Parent = x;

        y.Parent = x.Parent;
        if (x.Parent == null)
            Root = y;
        else if (x == x.Parent.Left)
            x.Parent.Left = y;
        else
            x.Parent.Right = y;

        y.Left = x;
        x.Parent = y;

        UpdateDepth(x);
        UpdateDepth(y);
    }

    protected void RotateRight(TNode y)
    {
        if (y == null || y.Left == null) return;

        TNode x = y.Left;

        y.Left = x.Right;
        if (x.Right != null) x.Right.Parent = y;

        x.Parent = y.Parent;
        if (y.Parent == null)
            Root = x;
        else if (y == y.Parent.Left)
            y.Parent.Left = x;
        else
            y.Parent.Right = x;

        x.Right = y;
        y.Parent = x;

        UpdateDepth(y);
        UpdateDepth(x);
    }
    
    protected void RotateBigLeft(TNode x)
    {
        RotateRight(x.Left!);
        RotateLeft(x);
    }
    
    protected void RotateBigRight(TNode y)
    {
        RotateLeft(y.Right!);
        RotateRight(y);
    }
    
    protected void RotateDoubleLeft(TNode x)
    {
        RotateLeft(x);
        RotateLeft(x);
    }

    protected void RotateDoubleRight(TNode y)
    {
        RotateRight(y);
        RotateRight(y);
    }
    
    protected void Transplant(TNode u, TNode? v)
    {
        if (u.Parent == null)
        {
            Root = v;
        }
        else if (u.IsLeftChild)
        {
            u.Parent.Left = v;
        }
        else
        {
            u.Parent.Right = v;
        }
        v?.Parent = u.Parent;
    }
    #endregion
    
    public IEnumerable<TreeEntry<TKey, TValue>> InOrder() 
        => new TreeIterator(Root, TraversalStrategy.InOrder);

    public IEnumerable<TreeEntry<TKey, TValue>> PreOrder() 
        => new TreeIterator(Root, TraversalStrategy.PreOrder);

    public IEnumerable<TreeEntry<TKey, TValue>> PostOrder() 
        => new TreeIterator(Root, TraversalStrategy.PostOrder);

    public IEnumerable<TreeEntry<TKey, TValue>> InOrderReverse() 
        => new TreeIterator(Root, TraversalStrategy.InOrderReverse);

    public IEnumerable<TreeEntry<TKey, TValue>> PreOrderReverse() 
        => new TreeIterator(Root, TraversalStrategy.PreOrderReverse);

    public IEnumerable<TreeEntry<TKey, TValue>> PostOrderReverse() 
        => new TreeIterator(Root, TraversalStrategy.PostOrderReverse);
    
    /// <summary>
    /// Внутренний класс-итератор. 
    /// Реализует паттерн Iterator вручную, без yield return (ban).
    /// </summary>
    private struct TreeIterator :
    IEnumerable<TreeEntry<TKey, TValue>>,
    IEnumerator<TreeEntry<TKey, TValue>>
    {
        private readonly TNode? _root;
        private readonly TraversalStrategy _strategy;
        private Stack<TNode> _stack;
        private TNode? _current;
        private TNode? _lastVisited;

        public TreeIterator(TNode? root, TraversalStrategy strategy)
        {
            _root = root;
            _strategy = strategy;
            _stack = new Stack<TNode>();
            _current = null;
            _lastVisited = null;
            Initialize();
        }

        private void Initialize()
        {
            _stack.Clear();
            _current = null;
            _lastVisited = null;

            if (_root == null) return;

            switch (_strategy)
            {
                case TraversalStrategy.InOrder:
                case TraversalStrategy.InOrderReverse:
                    PushLeftOrRight(_root);
                    break;

                case TraversalStrategy.PreOrder:
                case TraversalStrategy.PreOrderReverse:
                    _stack.Push(_root);
                    break;

                case TraversalStrategy.PostOrder:
                case TraversalStrategy.PostOrderReverse:
                    _current = _root;
                    break;
            }
        }

        private void PushLeftOrRight(TNode? node)
        {
            if (node == null) return;

            bool goLeft = _strategy == TraversalStrategy.InOrder;

            TNode? next = node;
            while (next != null)
            {
                _stack.Push(next);
                next = goLeft ? next.Left : next.Right;
            }
        }

        public IEnumerator<TreeEntry<TKey, TValue>> GetEnumerator()
        {
            return new TreeIterator(_root, _strategy);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public TreeEntry<TKey, TValue> Current
        {
            get
            {
                if (_current == null)
                    throw new InvalidOperationException("not an element.");

                return new TreeEntry<TKey, TValue>(
                    _current.Key,
                    _current.Value,
                    _current.Depth  
                );
            }
        }

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            switch (_strategy)
            {
                case TraversalStrategy.InOrder:         return MoveNextInOrder();
                case TraversalStrategy.InOrderReverse:  return MoveNextInOrderReverse();
                case TraversalStrategy.PreOrder:        return MoveNextPreOrder();
                case TraversalStrategy.PreOrderReverse: return MoveNextPreOrderReverse();
                case TraversalStrategy.PostOrder:       return MoveNextPostOrder();
                case TraversalStrategy.PostOrderReverse:return MoveNextPostOrderReverse();
                default: return false;
            }
        }

        private bool MoveNextInOrder()
        {
            if (_stack.Count == 0) return false;

            _current = _stack.Pop();

            if (_current.Right != null)
            {
                PushLeftOrRight(_current.Right);
            }

            return true;
        }

        private bool MoveNextInOrderReverse()
        {
            if (_stack.Count == 0) return false;

            _current = _stack.Pop();

            if (_current.Left != null)
            {
                PushLeftOrRight(_current.Left);
            }

            return true;
        }

        private bool MoveNextPreOrder()
        {
            if (_stack.Count == 0) return false;

            _current = _stack.Pop();

            if (_current.Right != null) _stack.Push(_current.Right);
            if (_current.Left  != null) _stack.Push(_current.Left);

            return true;
        }

        private bool MoveNextPreOrderReverse()
        {
            if (_stack.Count == 0) return false;

            _current = _stack.Pop();

            if (_current.Left  != null) _stack.Push(_current.Left);
            if (_current.Right != null) _stack.Push(_current.Right);

            return true;
        }

        private bool MoveNextPostOrder()
        {
            while (_current != null || _stack.Count > 0)
            {
                if (_current != null)
                {
                    _stack.Push(_current);
                    _current = _current.Left;
                }
                else
                {
                    TNode peek = _stack.Peek();

                    if (peek.Right != null && _lastVisited != peek.Right)
                    {
                        _current = peek.Right;
                    }
                    else
                    {
                        _current = _stack.Pop();
                        _lastVisited = _current;
                        return true;
                    }
                }
            }

            _current = null;
            return false;
        }

        private bool MoveNextPostOrderReverse()
        {
            while (_current != null || _stack.Count > 0)
            {
                if (_current != null)
                {
                    _stack.Push(_current);
                    _current = _current.Right;
                }
                else
                {
                    TNode peek = _stack.Peek();

                    if (peek.Left != null && _lastVisited != peek.Left)
                    {
                        _current = peek.Left;
                    }
                    else
                    {
                        _current = _stack.Pop();
                        _lastVisited = _current;
                        return true;
                    }
                }
            }

            _current = null;
            return false;
        }

        public void Reset()
        {
            Initialize();
        }

        public void Dispose()
        {
            _stack.Clear();
        }
    }
    
    
    private enum TraversalStrategy { InOrder, PreOrder, PostOrder, InOrderReverse, PreOrderReverse, PostOrderReverse }
    
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        throw new NotImplementedException();
    }
    
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


    public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);
    public void Clear() { Root = null; Count = 0; }
    public bool Contains(KeyValuePair<TKey, TValue> item) => ContainsKey(item.Key);
    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => throw new NotImplementedException();
    public bool Remove(KeyValuePair<TKey, TValue> item) => Remove(item.Key);
}