using Clipper2Lib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DeploySharp.Data
{
    /// <summary>
    /// Represents an immutable generic pair of values with multiple functional interfaces
    /// 表示具有多个功能接口的不可变泛型值对
    /// </summary>
    /// <typeparam name="T1">The type of the first element/第一个元素的类型</typeparam>
    /// <typeparam name="T2">The type of the second element/第二个元素的类型</typeparam>
    /// <remarks>
    /// <para>
    /// Provides implementations for equality comparison, sorting, enumeration and tuple conversion.
    /// 提供相等比较、排序、枚举和元组转换的实现。
    /// </para>
    /// <para>
    /// Supports pattern matching through deconstruction.
    /// 通过解构支持模式匹配。
    /// </para>
    /// </remarks>
    [Serializable]
    public class Pair<T1, T2> :
        IEquatable<Pair<T1, T2>>,     // Supports equality comparison/支持相等比较
        IComparable<Pair<T1, T2>>,    // Supports sorting/支持排序
        IEnumerable<object>           // Supports enumeration/支持枚举
    {
        /// <summary>
        /// Gets or sets the first element of the pair
        /// 获取或设置值对的第一个元素
        /// </summary>
        public T1 First { get; set; }

        /// <summary>
        /// Gets or sets the second element of the pair
        /// 获取或设置值对的第二个元素
        /// </summary>
        public T2 Second { get; set; }

        /// <summary>
        /// Initializes a new instance of the Pair class
        /// 初始化Pair类的新实例
        /// </summary>
        public Pair() { }

        /// <summary>
        /// Initializes a new instance of the Pair class with specified values
        /// 使用指定值初始化Pair类的新实例
        /// </summary>
        /// <param name="first">The first element/第一个元素</param>
        /// <param name="second">The second element/第二个元素</param>
        public Pair(T1 first, T2 second)
        {
            First = first;
            Second = second;
        }

        /// <summary>
        /// Deconstructs the pair into individual components
        /// 将值对解构为单独组件
        /// </summary>
        /// <param name="first">Output parameter for first element/第一个元素的输出参数</param>
        /// <param name="second">Output parameter for second element/第二个元素的输出参数</param>
        public void Deconstruct(out T1 first, out T2 second)
        {
            first = First;
            second = Second;
        }

        /// <summary>
        /// Returns an enumerator that iterates through the pair elements
        /// 返回遍历值对元素的枚举器
        /// </summary>
        /// <returns>An enumerator for the pair elements/值对元素的枚举器</returns>
        public IEnumerator<object> GetEnumerator()
        {
            yield return First;
            yield return Second;
        }

        /// <summary>
        /// Returns a non-generic enumerator that iterates through the pair elements
        /// 返回遍历值对元素的非泛型枚举器
        /// </summary>
        /// <returns>A non-generic enumerator/非泛型枚举器</returns>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Returns a string that represents the current pair
        /// 返回表示当前值对的字符串
        /// </summary>
        /// <returns>A formatted string showing both elements/显示两个元素的格式化字符串</returns>
        public override string ToString() => $"({First}, {Second})";

        /// <summary>
        /// Determines whether the specified object is equal to the current pair
        /// 确定指定的对象是否等于当前值对
        /// </summary>
        /// <param name="obj">The object to compare/要比较的对象</param>
        /// <returns>true if equal; otherwise false/如果相等则为true；否则为false</returns>
        public override bool Equals(object obj) =>
            obj is Pair<T1, T2> other && Equals(other);

        /// <summary>
        /// Determines whether the specified pair is equal to the current pair
        /// 确定指定的值对是否等于当前值对
        /// </summary>
        /// <param name="other">The pair to compare/要比较的值对</param>
        /// <returns>true if equal; otherwise false/如果相等则为true；否则为false</returns>
        public bool Equals(Pair<T1, T2> other) =>
            EqualityComparer<T1>.Default.Equals(First, other.First) &&
            EqualityComparer<T2>.Default.Equals(Second, other.Second);

        /// <summary>
        /// Compares the current pair with another pair
        /// 将当前值对与另一个值对进行比较
        /// </summary>
        /// <param name="other">The pair to compare/要比较的值对</param>
        /// <returns>
        /// A value indicating their relative order/指示它们相对顺序的值
        /// </returns>
        public int CompareTo(Pair<T1, T2> other)
        {
            var firstComparison = Comparer<T1>.Default.Compare(First, other.First);
            return firstComparison != 0 ? firstComparison :
                Comparer<T2>.Default.Compare(Second, other.Second);
        }

        /// <summary>
        /// Converts a tuple implicitly to a Pair
        /// 隐式将元组转换为Pair
        /// </summary>
        /// <param name="tuple">The tuple to convert/要转换的元组</param>
        public static implicit operator Pair<T1, T2>((T1, T2) tuple) =>
            new Pair<T1, T2>(tuple.Item1, tuple.Item2);

        /// <summary>
        /// Converts a Pair implicitly to a tuple
        /// 隐式将Pair转换为元组
        /// </summary>
        /// <param name="pair">The pair to convert/要转换的值对</param>
        public static implicit operator (T1, T2)(Pair<T1, T2> pair) =>
            (pair.First, pair.Second);

        /// <summary>
        /// Determines whether two pairs are equal
        /// 确定两个值对是否相等
        /// </summary>
        /// <param name="left">The first pair to compare/要比较的第一个值对</param>
        /// <param name="right">The second pair to compare/要比较的第二个值对</param>
        /// <returns>true if equal; otherwise false/如果相等则为true；否则为false</returns>
        public static bool operator ==(Pair<T1, T2> left, Pair<T1, T2> right) =>
            left?.Equals(right) ?? right is null;

        /// <summary>
        /// Determines whether two pairs are not equal
        /// 确定两个值对是否不相等
        /// </summary>
        /// <param name="left">The first pair to compare/要比较的第一个值对</param>
        /// <param name="right">The second pair to compare/要比较的第二个值对</param>
        /// <returns>true if not equal; otherwise false/如果不相等则为true；否则为false</returns>
        public static bool operator !=(Pair<T1, T2> left, Pair<T1, T2> right) =>
            !(left == right);

    }

    /// <summary>
    /// Provides extension methods for Pair objects
    /// 为Pair对象提供扩展方法
    /// </summary>
    public static class PairExtensions
    {
        /// <summary>
        /// Extends a Pair to a Triplet by adding a third element
        /// 通过添加第三个元素将Pair扩展为Triplet
        /// </summary>
        /// <typeparam name="T1">Type of first element/第一个元素的类型</typeparam>
        /// <typeparam name="T2">Type of second element/第二个元素的类型</typeparam>
        /// <typeparam name="T3">Type of third element/第三个元素的类型</typeparam>
        /// <param name="pair">The pair to extend/要扩展的值对</param>
        /// <param name="third">The third element to add/要添加的第三个元素</param>
        /// <returns>A new Triplet containing all three elements/包含所有三个元素的新Triplet</returns>
        public static Triplet<T1, T2, T3> With<T1, T2, T3>(
            this Pair<T1, T2> pair, T3 third) =>
            new Triplet<T1, T2, T3>(pair.First, pair.Second, third);
    }

    /// <summary>
    /// Represents an immutable generic triplet of values
    /// 表示不可变泛型三元组
    /// </summary>
    /// <typeparam name="T1">Type of first element/第一个元素的类型</typeparam>
    /// <typeparam name="T2">Type of second element/第二个元素的类型</typeparam>
    /// <typeparam name="T3">Type of third element/第三个元素的类型</typeparam>
    /// <remarks>
    /// Inherits all functionality from Pair while adding third element support.
    /// 继承Pair的所有功能，同时添加第三个元素支持。
    /// </remarks>
    public class Triplet<T1, T2, T3> : Pair<T1, T2>
    {
        /// <summary>
        /// Gets or sets the third element of the triplet
        /// 获取或设置三元组的第三个元素
        /// </summary>
        public T3 Third { get; set; }

        /// <summary>
        /// Initializes a new instance of the Triplet class
        /// 初始化Triplet类的新实例
        /// </summary>
        /// <param name="first">The first element/第一个元素</param>
        /// <param name="second">The second element/第二个元素</param>
        /// <param name="third">The third element/第三个元素</param>
        public Triplet(T1 first, T2 second, T3 third) : base(first, second)
        {
            Third = third;
        }

        /// <summary>
        /// Deconstructs the triplet into individual components
        /// 将三元组解构为单独组件
        /// </summary>
        /// <param name="first">Output parameter for first element/第一个元素的输出参数</param>
        /// <param name="second">Output parameter for second element/第二个元素的输出参数</param>
        /// <param name="third">Output parameter for third element/第三个元素的输出参数</param>
        public void Deconstruct(out T1 first, out T2 second, out T3 third)
        {
            base.Deconstruct(out first, out second);
            third = Third;
        }

        /// <summary>
        /// Returns a string that represents the current triplet
        /// 返回表示当前三元组的字符串
        /// </summary>
        /// <returns>A formatted string showing all three elements/显示三个元素的格式化字符串</returns>
        public override string ToString() => $"({First}, {Second}, {Third})";

        /// <summary>
        /// Determines whether the specified object is equal to the current triplet
        /// 确定指定的对象是否等于当前三元组
        /// </summary>
        public override bool Equals(object obj) =>
            obj is Triplet<T1, T2, T3> other &&
            base.Equals(other) &&
            EqualityComparer<T3>.Default.Equals(Third, other.Third);

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
