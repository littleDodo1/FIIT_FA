using System;
using TreeDataStructures.Implementations.BST;

class Program
{
    static void Main(string[] args)
    {
        var tree = new BinarySearchTree<int, string>();

        // Добавляем элементы
        int[] values = [50, 30, 70, 20, 40, 60, 80, 10, 25, 35, 45, 55, 65, 75, 85];
        foreach (var v in values)
        {
            tree.Add(v, $"Value {v}");
        }

        Console.WriteLine($"Количество элементов: {tree.Count}\n");

        Console.WriteLine("InOrder (отсортировано):");
        foreach (var entry in tree.InOrder())
        {
            Console.WriteLine($"  {entry.Key} → {entry.Value}, Depth = {entry.Depth}");
        }

        Console.WriteLine("\nPreOrder:");
        foreach (var entry in tree.PreOrder())
        {
            Console.WriteLine($"  {entry.Key} (Depth: {entry.Depth})");
        }

        Console.WriteLine("\nУдаляем 30 и 70...");
        tree.Remove(30);
        tree.Remove(70);

        Console.WriteLine("\nInOrder после удаления:");
        foreach (var entry in tree.InOrder())
        {
            Console.WriteLine($"  {entry.Key} → {entry.Value}, Depth = {entry.Depth}");
        }

        Console.WriteLine($"\nКоличество после удаления: {tree.Count}");
    }
}