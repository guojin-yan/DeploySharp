using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    /// <summary>
    /// Provides utility methods for data processing operations
    /// 提供数据处理操作的实用方法
    /// </summary>
    /// <remarks>
    /// <para>
    /// Contains common helper methods used across the data processing pipeline,
    /// including array search operations and statistical functions.
    /// </para>
    /// <para>
    /// 包含数据处理管道中使用的常见辅助方法，
    /// 包括数组搜索操作和统计函数。
    /// </para>
    /// </remarks>
    public static class Util
    {
        /// <summary>
        /// Finds the maximum value and its relative index within a specified range of a generic array.
        /// 在泛型数组的指定范围内查找最大值及其相对索引。
        /// </summary>
        /// <typeparam name="T">
        /// The type of array elements. Must implement IComparable&lt;T&gt; interface.
        /// 数组元素的类型。必须实现 IComparable&lt;T&gt; 接口。
        /// </typeparam>
        /// <param name="array">
        /// The array to search in. 要搜索的数组。
        /// </param>
        /// <param name="startIndex">
        /// The starting index of the search range (inclusive).
        /// 搜索范围的起始索引（包含）。
        /// </param>
        /// <param name="endIndex">
        /// The ending index of the search range (inclusive).
        /// 搜索范围的结束索引（包含）。
        /// </param>
        /// <returns>
        /// A tuple containing the maximum value and its relative index within the range.
        /// 一个元组，包含最大值及其在范围内的相对索引。
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown when array is null or empty.
        /// 当数组为null或空时抛出。
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when start or end index is out of valid range.
        /// 当起始或结束索引超出有效范围时抛出。
        /// </exception>
        /// <example>
        /// <code>
        /// float[] values = { 0.1f, 0.5f, 0.9f, 0.3f, 0.7f };
        /// var (maxVal, idx) = Util.FindMaxInRange(values, 1, 3);
        /// // maxVal = 0.9f, idx = 1 (relative to startIndex)
        /// </code>
        /// </example>
        public static (T MaxValue, int Index) FindMaxInRange<T>(T[] array, int startIndex, int endIndex) where T : IComparable<T>
        {
            // 1. Parameter validation
            // 1. 参数校验
            if (array == null || array.Length == 0)
                throw new ArgumentException("Array cannot be null or empty.", nameof(array));
            if (startIndex < 0 || endIndex > array.Length || startIndex > endIndex)
                throw new ArgumentOutOfRangeException("Invalid start or end index.", nameof(startIndex));
            
            // 2. Initialize max value and its index (using first element in range)
            // 2. 初始化最大值和其索引（使用范围内的第一个元素）
            T maxValue = array[startIndex];
            int maxIndex = startIndex;
            
            // 3. Loop comparison starting from startIndex
            // 3. 从 startIndex 开始循环比较
            for (int i = startIndex; i < endIndex; i++)
            {
                // Compare using CompareTo
                // 使用 CompareTo 进行比较
                if (array[i].CompareTo(maxValue) > 0)
                {
                    maxValue = array[i];
                    maxIndex = i; // maxIndex is always the absolute index in original array
                }
            }
            
            // 4. Return result (return relative index: maxIndex - startIndex)
            // 4. 返回结果（返回相对索引：maxIndex - startIndex）
            return (maxValue, maxIndex - startIndex);
        }

        /// <summary>
        /// Finds the minimum value and its relative index within a specified range of a generic array.
        /// 在泛型数组的指定范围内查找最小值及其相对索引。
        /// </summary>
        /// <typeparam name="T">
        /// The type of array elements. Must implement IComparable&lt;T&gt; interface.
        /// 数组元素的类型。必须实现 IComparable&lt;T&gt; 接口。
        /// </typeparam>
        /// <param name="array">
        /// The array to search in. 要搜索的数组。
        /// </param>
        /// <param name="startIndex">
        /// The starting index of the search range (inclusive).
        /// 搜索范围的起始索引（包含）。
        /// </param>
        /// <param name="endIndex">
        /// The ending index of the search range (inclusive).
        /// 搜索范围的结束索引（包含）。
        /// </param>
        /// <returns>
        /// A tuple containing the minimum value and its relative index within the range.
        /// 一个元组，包含最小值及其在范围内的相对索引。
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown when array is null or empty.
        /// 当数组为null或空时抛出。
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when start or end index is out of valid range.
        /// 当起始或结束索引超出有效范围时抛出。
        /// </exception>
        /// <example>
        /// <code>
        /// float[] values = { 0.9f, 0.5f, 0.1f, 0.3f, 0.7f };
        /// var (minVal, idx) = Util.FindMinInRange(values, 1, 3);
        /// // minVal = 0.1f, idx = 1 (relative to startIndex)
        /// </code>
        /// </example>
        public static (T MinValue, int Index) FindMinInRange<T>(T[] array, int startIndex, int endIndex) where T : IComparable<T>
        {
            // 1. Parameter validation
            // 1. 参数校验
            if (array == null || array.Length == 0)
                throw new ArgumentException("Array cannot be null or empty.", nameof(array));
            if (startIndex < 0 || endIndex >= array.Length || startIndex > endIndex)
                throw new ArgumentOutOfRangeException("Invalid start or end index.", nameof(startIndex));
            
            // 2. Initialize min value and its index (using first element in range)
            // 2. 初始化最小值和其索引（使用范围内的第一个元素）
            T minValue = array[startIndex];
            int minIndex = startIndex;
            
            // 3. Loop comparison starting from startIndex + 1
            // 3. 从 startIndex + 1 开始循环比较
            for (int i = startIndex + 1; i <= endIndex; i++)
            {
                // Compare using CompareTo
                // 使用 CompareTo 进行比较
                if (array[i].CompareTo(minValue) < 0)
                {
                    minValue = array[i];
                    minIndex = i; // minIndex is always the absolute index in original array
                }
            }
            
            // 4. Return result (return relative index: minIndex - startIndex)
            // 4. 返回结果（返回相对索引：minIndex - startIndex）
            return (minValue, minIndex - startIndex);
        }
    }
}
