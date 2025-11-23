using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    public static class Util
    {
        /// <summary>
        /// 在泛型数组的指定范围内查找最大值及其在原始数组中的索引。
        /// </summary>
        /// <typeparam name="T">必须实现 IComparable<T> 接口的类型。</typeparam>
        /// <param name="array">要搜索的数组。</param>
        /// <param name="startIndex">搜索范围的起始索引（包含）。</param>
        /// <param name="endIndex">搜索范围的结束索引（包含）。</param>
        /// <returns>一个元组，包含最大值和它在原始数组中的索引。</returns>
        public static (T MaxValue, int Index) FindMaxInRange<T>(T[] array, int startIndex, int endIndex) where T : IComparable<T>
        {
            // 1. 参数校验
            if (array == null || array.Length == 0)
                throw new ArgumentException("Array cannot be null or empty.", nameof(array));
            if (startIndex < 0 || endIndex > array.Length || startIndex > endIndex)
                throw new ArgumentOutOfRangeException("Invalid start or end index.", nameof(startIndex));
            // 2. 初始化最大值和其索引（使用范围内的第一个元素）
            T maxValue = array[startIndex];
            int maxIndex = startIndex;
            // 3. 从 startIndex + 1 开始循环比较
            for (int i = startIndex ; i < endIndex; i++)
            {
                // 使用 CompareTo 进行比较
                if (array[i].CompareTo(maxValue) > 0)
                {
                    maxValue = array[i];
                    maxIndex = i; // maxIndex 始终是原始数组中的绝对索引
                }
            }
            // 4. 返回结果（直接返回 maxIndex，因为它已经是正确的绝对索引）
            return (maxValue, maxIndex - startIndex);
        }

        /// <summary>
        /// 在泛型数组的指定范围内查找最小值及其在原始数组中的索引。
        /// </summary>
        /// <typeparam name="T">必须实现 IComparable<T> 接口的类型。</typeparam>
        /// <param name="array">要搜索的数组。</param>
        /// <param name="startIndex">搜索范围的起始索引（包含）。</param>
        /// <param name="endIndex">搜索范围的结束索引（包含）。</param>
        /// <returns>一个元组，包含最大值和它在原始数组中的索引。</returns>
        public static (T MaxValue, int Index) FindMinInRange<T>(T[] array, int startIndex, int endIndex) where T : IComparable<T>
        {
            // 1. 参数校验
            if (array == null || array.Length == 0)
                throw new ArgumentException("Array cannot be null or empty.", nameof(array));
            if (startIndex < 0 || endIndex >= array.Length || startIndex > endIndex)
                throw new ArgumentOutOfRangeException("Invalid start or end index.", nameof(startIndex));
            // 2. 初始化最大值和其索引（使用范围内的第一个元素）
            T maxValue = array[startIndex];
            int maxIndex = startIndex;
            // 3. 从 startIndex + 1 开始循环比较
            for (int i = startIndex + 1; i <= endIndex; i++)
            {
                // 使用 CompareTo 进行比较
                if (array[i].CompareTo(maxValue) < 0)
                {
                    maxValue = array[i];
                    maxIndex = i; // maxIndex 始终是原始数组中的绝对索引
                }
            }
            // 4. 返回结果（直接返回 maxIndex，因为它已经是正确的绝对索引）
            return (maxValue, maxIndex - startIndex);
        }
    }
}
