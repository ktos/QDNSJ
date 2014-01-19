using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace QDNSJ
{
    class Converters
    {
        public static string ConvertStringValue(string value)
        {
            return value.Replace('\'', '"');
        }

        public static string ConvertAssigment(string value)
        {
            return value.Replace(":=", "=");
        }

        public static string ConvertLogic(string value)
        {
            value = value.Replace("and", "&&");
            value = value.Replace("or", "||");
            value = value.Replace("=", "==");
            value = value.Replace("<==", "<=");
            value = value.Replace(">==", ">=");
            value = value.Replace("<>", "!=");

            return value;
        }

        public static string ConvertOperators(string value)
        {
            Regex b = new Regex("(.*):=(.*)");
            Regex div = new Regex("(.*) div (.*)");

            value = value.Replace(" mod ", " % ");

            var group2 = b.Match(value);
            if (group2.Success)
            {
                var s = group2.Groups[2].ToString().Trim();

                while (s.IndexOf(" div ") > -1)
                {
                    s = div.Replace(s, x =>
                    {
                        if (x.Success)
                        {
                            return "Math.floor(" + x.Groups[1] + " / " + x.Groups[2] + ")";
                        }
                        else
                            return x.Value;
                    });
                }

                value = group2.Groups[1].ToString().Trim() + " = " + s;
            }
            
            return value;
        }

        public static string ConvertBuiltInFunctions(string value)
        {
            // TODO: sin, abs, i takie tam
            value = value.Replace("sqrt", "Math.sqrt");
            return value;
        }

        public static string ConvertFunctionHeader(string value)
        {
            // TODO: obsługa przekazywania przez referencję

            if (value.IndexOf('(') == -1)
                return value + "()";

            return value;
        }
    }

    enum Compatibility
    {
        BROWSER = 0,
        SPIDERMONKEY = 1
    }


    class QdNsToJsConverter
    {
        private XDocument nss;
        private Compatibility compat = Compatibility.BROWSER;
        private TextWriter writer = Console.Out;
        private int depth;

        public QdNsToJsConverter(string path)
        {
            nss = XDocument.Load(path);
        }

        public QdNsToJsConverter(string path, Compatibility com)
            : this(path)
        {
            compat = com;
        }

        public QdNsToJsConverter(string path, Compatibility com, TextWriter writer)
            : this(path, com)
        {
            this.writer = writer;
        }

        public void Parse()
        {
            this.depth = 0;

            switch (nss.Document.Root.Attribute("type").Value.ToString())
            {
                case "sequence":
                    {
                        // wyświetlanie autora/komentarzy?
                        break;
                    }

                case "function":
                    {
                        write("function {0} {{", Converters.ConvertFunctionHeader(nss.Document.Root.Element("name").Value));
                        this.depth = 1;
                        write("var returnVal;");
                        break;
                    }

                case "procedure":
                    {
                        write("function {0} {{", Converters.ConvertFunctionHeader(nss.Document.Root.Element("name").Value));
                        this.depth = 1;
                        break;
                    }

                default:
                    {
                        throw new NotSupportedException();
                    }
            }

            parseVariables();

            var sequence = nss.Document.Root.Element("sequence");
            parseSequence(sequence);

            switch (nss.Document.Root.Attribute("type").Value.ToString())
            {
                case "function":
                    {
                        this.depth = 1;
                        write("");
                        write("return returnVal;");
                        this.depth = 0;
                        write("}}");
                        break;
                    }
                case "procedure":
                    {
                        this.depth = 0;
                        write("}}");
                        break;
                    }
            }

        }

        private void parseVariables()
        {
            // TODO: obsługa tablic wielowymiarowych

            foreach (var v in nss.Document.Root.Element("variables").Elements("variable"))
            {
                if (v.Element("type").Element("long") != null)
                    write("var {0} = {1};", v.Element("name").Value, v.Element("value").Value);
                else // tablica
                {
                    string values = "";
                    foreach (var vv in v.Element("value").Elements("element"))
                    {
                        values += vv.Value + ", ";
                    }
                    values = values.Substring(0, values.Length - 2);

                    write("var {0} = [{1}];", v.Element("name").Value, values);
                }
            }
            write("");
        }

        private string intendation()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < this.depth; i++)
            {
                sb.Append("    ");
            }

            return sb.ToString();
        }

        private void write(string text, params object[] obj)
        {
            var r = String.Format(text, obj);
            if (depth > 0)
                writer.Write(intendation());
            writer.WriteLine(r);
        }

        private void parseSequence(XElement sequence)
        {
            foreach (var i in sequence.Elements())
            {
                parseComment(i);

                switch (i.Name.ToString())
                {
                    case "simpleinstruction":
                        {
                            parseSimpleInstruction(i);
                            break;
                        }

                    case "inputinstruction":
                        {
                            parseInputInstruction(i);
                            break;
                        }

                    case "outputinstruction":
                        {
                            parseOutputInstruction(i);

                            break;
                        }

                    case "selection":
                        {
                            parseSelection(i);
                            break;
                        }

                    case "iteration":
                        {
                            parseIteration(i);
                            break;
                        }

                    case "returninstruction":
                        {
                            parseReturnInstruction(i);
                            break;
                        }

                    case "callinstruction":
                        {
                            parseCallInstruction(i);
                            break;
                        }

                    default:
                        {
                            Console.Error.WriteLine("Unrecognized instruction: " + i.Name);
                            break;
                        }
                }
            }

            if (this.depth > 0)
                this.depth--;
        }

        private void parseCallInstruction(XElement i)
        {
            var value = Converters.ConvertBuiltInFunctions(i.Element("text").Value);
            value = Converters.ConvertOperators(value);
            value = Converters.ConvertFunctionHeader(value);
            write("{0};", value);
        }

        private void parseReturnInstruction(XElement i)
        {
            var value = Converters.ConvertBuiltInFunctions(i.Element("text").Value);
            value = Converters.ConvertOperators(value);
            write("returnVal = {0};", value);
        }

        private void parseIteration(XElement i)
        {
            var value = Converters.ConvertLogic(i.Element("condition").Element("text").Value);
            value = Converters.ConvertBuiltInFunctions(value);
            value = Converters.ConvertOperators(value);

            write("while ({0}) {{", value);

            this.depth++;
            parseSequence(i.Element("loop").Element("sequence"));

            write("}}", depth);
        }

        private void parseSelection(XElement i)
        {
            var value = Converters.ConvertLogic(i.Element("condition").Element("text").Value);
            value = Converters.ConvertBuiltInFunctions(value);
            value = Converters.ConvertOperators(value);

            write("if ({0}) {{", value);

            this.depth++;
            parseSequence(i.Element("ontrue").Element("sequence"));

            if (i.Element("onfalse").Element("sequence").Elements().Count() > 0)
            {
                write("}} else {{");

                this.depth++;
                parseSequence(i.Element("onfalse").Element("sequence"));
            }

            write("}}");

        }

        private void parseOutputInstruction(XElement i)
        {
            // BUG: przecinek wewnątrz stringa
            string f = "alert({0});";
            if (compat == Compatibility.SPIDERMONKEY)
                f = "print({0});";

            if (i.Element("text").Value.IndexOf(',') > -1)
            {
                var s = i.Element("text").Value.Split(',');
                foreach (var v in s)
                {
                    write(f, Converters.ConvertStringValue(v.Trim()));
                }
            }
            else
                write(f, Converters.ConvertStringValue(i.Element("text").Value));
        }

        private void parseInputInstruction(XElement i)
        {
            string f = "{0} = parseFloat(prompt());";
            if (compat == Compatibility.SPIDERMONKEY)
                f = "{0} = parseFloat(readline());";

            if (i.Element("text").Value.IndexOf(',') > -1)
            {
                var s = i.Element("text").Value.Split(',');
                foreach (var v in s)
                {
                    write(f, v.Trim());
                }
            }
            else
                write(f, i.Element("text").Value);
        }

        private void parseSimpleInstruction(XElement i)
        {
            var value = Converters.ConvertBuiltInFunctions(i.Element("text").Value);
            value = Converters.ConvertOperators(value);

            write("{0};", Converters.ConvertAssigment(value));
        }

        private void parseComment(XElement i)
        {
            if (i.Element("comment") != null && i.Element("comment").Value.Length > 0)
                write("// {0}", i.Element("comment").Value);
        }
    }
}
