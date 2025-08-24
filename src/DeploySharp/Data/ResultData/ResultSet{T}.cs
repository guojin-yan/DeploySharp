using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{

    public class ResultSet<ResultUnit> :  IEnumerable<ResultUnit> where ResultUnit : Result
    {
        public ResultUnit[] Predictions { get; set; }

        public ResultSet(ResultUnit[] predictions)
        {
            predictions = predictions ?? throw new ArgumentNullException(nameof(predictions));
        }

        public void UpdateCategory(string[] categorys)
        {
            foreach (var prediction in Predictions) 
            {
                prediction.UpdateCategory(categorys);
            }
        }
        // 索引器
        public ResultUnit this[int index] => Predictions[index];

        // 元素数量
        public int Count => Predictions.Length;

        // 默认ToString()
        public override string ToString()
        {
            return $"YoloResult<{typeof(ResultUnit).Name}> with {Count} predictions:\n" + string.Join(Environment.NewLine, Predictions.Select(r => r.ToString())); ;
        }

        #region Enumerator (迭代器实现)
        public IEnumerator<ResultUnit> GetEnumerator()
        {
            foreach (var item in Predictions)
            {
                yield return item;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        #endregion
    }
}
