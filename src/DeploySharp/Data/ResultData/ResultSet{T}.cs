using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{

    /// <summary>
    /// Represents a collection of prediction results with generic type support
    /// 表示具有泛型类型支持的预测结果集合
    /// </summary>
    /// <typeparam name="ResultUnit">
    /// The type of prediction result, must inherit from <see cref="Result"/>
    /// 预测结果的类型，必须继承自 <see cref="Result"/>
    /// </typeparam>
    /// <remarks>
    /// <para>
    /// This class provides enumeration capabilities and implements indexer access to the prediction collection.
    /// 此类提供枚举能力并实现对预测集合的索引器访问。
    /// </para>
    /// <para>
    /// The prediction results can be updated in batch operations.
    /// 预测结果可以通过批量操作进行更新。
    /// </para>
    /// </remarks>
    public class ResultSet<ResultUnit> : IEnumerable<ResultUnit> where ResultUnit : Result
    {
        /// <summary>
        /// Gets or sets the array of prediction results
        /// 获取或设置预测结果数组
        /// </summary>
        /// <value>
        /// An array containing all prediction results
        /// 包含所有预测结果的数组
        /// </value>
        public ResultUnit[] Predictions { get; set; }

        /// <summary>
        /// Initializes a new instance of the ResultSet class with specified predictions
        /// 使用指定的预测结果初始化 ResultSet 类的新实例
        /// </summary>
        /// <param name="predictions">The array of prediction results/预测结果数组</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when predictions parameter is null
        /// 当predictions参数为null时抛出
        /// </exception>
        public ResultSet(ResultUnit[] predictions)
        {
            Predictions = predictions ?? throw new ArgumentNullException(nameof(predictions));
        }

        /// <summary>
        /// Updates categories for all prediction results in the collection
        /// 为集合中的所有预测结果更新类别
        /// </summary>
        /// <param name="categorys">The new category names/新的类别名称数组</param>
        /// <remarks>
        /// Calls UpdateCategory() method for each prediction result in the collection
        /// 为集合中的每个预测结果调用UpdateCategory()方法
        /// </remarks>
        public void UpdateCategory(string[] categorys)
        {
            foreach (var prediction in Predictions)
            {
                prediction.UpdateCategory(categorys);
            }
        }

        /// <summary>
        /// Gets the prediction result at the specified index
        /// 获取指定索引处的预测结果
        /// </summary>
        /// <param name="index">The zero-based index of the prediction/预测结果的从零开始的索引</param>
        /// <returns>The prediction result at the specified index/指定索引处的预测结果</returns>
        public ResultUnit this[int index] => Predictions[index];

        /// <summary>
        /// Gets the number of prediction results in the collection
        /// 获取集合中预测结果的数量
        /// </summary>
        /// <value>
        /// The count of prediction results/预测结果的数量
        /// </value>
        public int Count => Predictions.Length;

        /// <summary>
        /// Returns a string that represents the current result set
        /// 返回表示当前结果集的字符串
        /// </summary>
        /// <returns>
        /// A formatted string showing the result type and all predictions
        /// 显示结果类型和所有预测结果的格式化字符串
        /// </returns>
        /// <example>
        /// <code>
        /// var resultSet = new ResultSet&lt;YoloResult&gt;(predictions);
        /// Console.WriteLine(resultSet.ToString());
        /// // Output: YoloResult&lt;YoloResult&gt; with 3 predictions:
        /// // [prediction1 details]
        /// // [prediction2 details]
        /// // [prediction3 details]
        /// </code>
        /// </example>
        public override string ToString()
        {
            return $"YoloResult<{typeof(ResultUnit).Name}> with {Count} predictions:\n" +
                   string.Join(Environment.NewLine, Predictions.Select(r => r.ToString()));
        }

        #region Enumerator (迭代器实现)

        /// <summary>
        /// Returns an enumerator that iterates through the prediction collection
        /// 返回一个遍历预测集合的枚举器
        /// </summary>
        /// <returns>An enumerator for prediction results/预测结果的枚举器</returns>
        public IEnumerator<ResultUnit> GetEnumerator()
        {
            foreach (var item in Predictions)
            {
                yield return item;
            }
        }

        /// <summary>
        /// Returns a non-generic enumerator that iterates through the prediction collection
        /// 返回一个遍历预测集合的非泛型枚举器
        /// </summary>
        /// <returns>A non-generic enumerator/非泛型枚举器</returns>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion
    }

}
