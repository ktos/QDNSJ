using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.IO;

namespace QDNSJ
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
                Console.WriteLine("Params?");
            else
            {
                var q = new QdNsToJsConverter(args[0], Compatibility.SPIDERMONKEY);
                q.Parse();
            }

            if (System.Diagnostics.Debugger.IsAttached)
                Console.ReadKey();
        }
    }
}
