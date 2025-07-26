using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    public class Pair<T1, T2>
    {


        public T1 First;
        public T2 Second;
        // 添加构造函数
        public Pair() { }
        public Pair(T1 first, T2 second)
        {
            First = first;
            Second = second;
        }
    }

}
