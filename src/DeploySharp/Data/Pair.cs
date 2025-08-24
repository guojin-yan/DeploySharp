using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DeploySharp.Data
{
    [Serializable]
    public class Pair<T1, T2> :
        IEquatable<Pair<T1, T2>>,     // 支持相等比较
        IComparable<Pair<T1, T2>>,    // 支持排序
        IEnumerable<object>           // 支持迭代
    {
        public T1 First { get; set; }
        public T2 Second { get; set; }

        // 构造函数
        public Pair() { }
        public Pair(T1 first, T2 second)
        {
            First = first;
            Second = second;
        }

        // 解构方法（支持元组式解构）
        public void Deconstruct(out T1 first, out T2 second)
        {
            first = First;
            second = Second;
        }

        // 实现 IEnumerable（允许 foreach 遍历）
        public IEnumerator<object> GetEnumerator()
        {
            yield return First;
            yield return Second;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        // 重写 ToString
        public override string ToString() => $"({First}, {Second})";

        // 重写 Equals 和 GetHashCode（用于字典/集合）
        public override bool Equals(object obj) =>
            obj is Pair<T1, T2> other && Equals(other);

        public bool Equals(Pair<T1, T2> other) =>
            EqualityComparer<T1>.Default.Equals(First, other.First) &&
            EqualityComparer<T2>.Default.Equals(Second, other.Second);


        // 实现 IComparable（按第一元素优先排序）
        public int CompareTo(Pair<T1, T2> other)
        {
            var firstComparison = Comparer<T1>.Default.Compare(First, other.First);
            return firstComparison != 0 ? firstComparison :
                Comparer<T2>.Default.Compare(Second, other.Second);
        }

        // 隐式转换（与元组互操作）
        public static implicit operator Pair<T1, T2>((T1, T2) tuple) =>
            new Pair<T1, T2>(tuple.Item1, tuple.Item2);

        public static implicit operator (T1, T2)(Pair<T1, T2> pair) =>
            (pair.First, pair.Second);

        // 运算符重载（== 和 !=）
        public static bool operator ==(Pair<T1, T2> left, Pair<T1, T2> right) =>
            left?.Equals(right) ?? right is null;

        public static bool operator !=(Pair<T1, T2> left, Pair<T1, T2> right) =>
            !(left == right);
    }
    public static class PairExtensions
    {
        // 将 Pair 扩展为 Triplet（三元组）
        public static Triplet<T1, T2, T3> With<T1, T2, T3>(
            this Pair<T1, T2> pair, T3 third) =>
            new Triplet<T1, T2, T3>(pair.First, pair.Second, third);
    }

    // 三元组实现
    public class Triplet<T1, T2, T3> : Pair<T1, T2>
    {
        public T3 Third { get; set; }

        public Triplet(T1 first, T2 second, T3 third) : base(first, second)
        {
            Third = third;
        }

        public void Deconstruct(out T1 first, out T2 second, out T3 third)
        {
            base.Deconstruct(out first, out second);
            third = Third;
        }

        public override string ToString() => $"({First}, {Second}, {Third})";
    }



        //var pair = new Pair<int, string>(1, "Apple");
        //var(id, name) = pair; // 解构
 
        //// 与元组互转
        //Pair<int, string> p2 = (2, "Banana");
        //(int num, string fruit) = p2;
 
        //// 比较
        //Console.WriteLine(pair == new Pair<int, string>(1, "Apple")); // True
 
        //// 排序
        //var list = new List<Pair<int, string>> { (2, "Banana"), (1, "Apple") };
        //list.Sort(); // 按 First 升序排列
 
        //// 三元组扩展
        //var triplet = pair.With(3.14); // Triplet<int, string, double>
}
