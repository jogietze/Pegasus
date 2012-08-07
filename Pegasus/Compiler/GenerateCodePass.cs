﻿// -----------------------------------------------------------------------
// <copyright file="GenerateCodePass.cs" company="(none)">
//   Copyright © 2012 John Gietzen.  All Rights Reserved.
//   This source is subject to the MIT license.
//   Please see license.txt for more information.
// </copyright>
// -----------------------------------------------------------------------

namespace Pegasus.Compiler
{
    using System.CodeDom.Compiler;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using Pegasus.Expressions;

    internal class GenerateCodePass : CompilePass
    {
        public override void Run(Grammar grammar, CompileResult result)
        {
            using (var stringWriter = new StringWriter())
            using (var codeWriter = new IndentedTextWriter(stringWriter))
            {
                new GenerateCodeExpressionTreeWlaker(result, codeWriter).WalkGrammar(grammar);
                result.Code = stringWriter.ToString();
            }
        }

        private class GenerateCodeExpressionTreeWlaker : ExpressionTreeWalker
        {
            private readonly IndentedTextWriter code;
            private int id;
            private readonly CompileResult result;

            public GenerateCodeExpressionTreeWlaker(CompileResult result, IndentedTextWriter codeWriter)
            {
                this.result = result;
                this.code = codeWriter;
            }

            private int Id
            {
                get { return id++; }
            }

            private string currentResultName = null;

            private static HashSet<string> keywords = new HashSet<string>
            {
                "abstract", "as", "base",
                "bool", "break", "byte",
                "case", "catch", "char",
                "checked", "class", "const",
                "continue", "decimal", "default",
                "delegate", "do", "double",
                "else", "enum", "event",
                "explicit", "extern", "false",
                "finally", "fixed", "float",
                "for", "foreach", "goto",
                "if", "implicit", "in",
                "int", "interface", "internal",
                "is", "lock", "long",
                "namespace", "new", "null",
                "object", "operator", "out",
                "override", "params", "private",
                "protected", "public", "readonly",
                "ref", "return", "sbyte",
                "sealed", "short", "sizeof",
                "stackalloc", "static", "string",
                "struct", "switch", "this",
                "throw", "true", "try",
                "typeof", "uint", "ulong",
                "unchecked", "unsafe", "ushort",
                "using", "virtual", "void",
                "volatile", "while",
            };

            private static string EscapeName(string name)
            {
                return keywords.Contains(name) ? "@" + name : name;
            }

            public override void WalkGrammar(Grammar grammar)
            {
                var assemblyName = Assembly.GetExecutingAssembly().GetName();
                this.code.WriteLine("// -----------------------------------------------------------------------");
                this.code.WriteLine("// <auto-generated>");
                this.code.WriteLine("// This code was generated by " + assemblyName.Name + " " + assemblyName.Version);
                this.code.WriteLine("//");
                this.code.WriteLine("// Changes to this file may cause incorrect behavior and will be lost if");
                this.code.WriteLine("// the code is regenerated.");
                this.code.WriteLine("// </auto-generated>");
                this.code.WriteLine("// -----------------------------------------------------------------------");
                this.code.WriteLineNoTabs("");

                this.code.WriteLine("namespace Test");
                this.code.WriteLine("{");
                this.code.Indent++;
                this.code.WriteLine("using System;");
                this.code.WriteLine("using System.Collections.Generic;");
                this.code.WriteLine("using Pegasus;");
                this.code.WriteLineNoTabs("");

                this.code.WriteLine("[System.CodeDom.Compiler.GeneratedCode(\"" + assemblyName.Name + "\", \"" + assemblyName.Version + "\")]");
                this.code.WriteLine("public partial class Parser");
                this.code.WriteLine("{");
                this.code.Indent++;
                this.code.WriteLine("private Cursor rightmostErrorCursor = null;");
                this.code.WriteLine("private List<string> rightmostErrors = new List<string>();");
                this.code.WriteLineNoTabs("");

                this.code.WriteLine("public string Parse(string subject)");
                this.code.WriteLine("{");
                this.code.Indent++;
                this.code.WriteLine("var cursor = new Cursor(subject, 0);");
                this.code.WriteLine("var result = this." + EscapeName(grammar.Rules[0].Name) + "(ref cursor);");
                this.code.WriteLine("if (result == null)");
                this.code.WriteLine("{");
                this.code.Indent++;
                this.code.WriteLine("throw new InvalidOperationException(\"Expected \" + string.Join(\", \", this.rightmostErrors) + \".\");");
                this.code.Indent--;
                this.code.WriteLine("}");
                this.code.WriteLine("return result.Value;");
                this.code.Indent--;
                this.code.WriteLine("}");

                base.WalkGrammar(grammar);

                this.code.WriteLineNoTabs("");
                this.code.WriteLine("private ParseResult<string> ParseLiteral(ref Cursor cursor, string literal, bool ignoreCase = false)");
                this.code.WriteLine("{");
                this.code.Indent++;
                this.code.WriteLine("if (cursor.Location + literal.Length <= cursor.Subject.Length)");
                this.code.WriteLine("{");
                this.code.Indent++;
                this.code.WriteLine("var substr = cursor.Subject.Substring(cursor.Location, literal.Length);");
                this.code.WriteLine("if (ignoreCase ? substr.Equals(literal, StringComparison.OrdinalIgnoreCase) : substr == literal)");
                this.code.WriteLine("{");
                this.code.Indent++;
                this.code.WriteLine("var result = new ParseResult<string>(substr.Length, substr);");
                this.code.WriteLine("cursor = cursor.Advance(result);");
                this.code.WriteLine("return result;");
                this.code.Indent--;
                this.code.WriteLine("}");
                this.code.Indent--;
                this.code.WriteLine("}");
                this.code.WriteLine("this.ReportError(cursor, \"'\" + literal + \"'\");");
                this.code.WriteLine("return null;");
                this.code.Indent--;
                this.code.WriteLine("}");

                this.code.WriteLineNoTabs("");
                this.code.WriteLine("private ParseResult<string> ParseClass(ref Cursor cursor, string characterRanges, bool negated = false, bool ignoreCase = false)");
                this.code.WriteLine("{");
                this.code.Indent++;
                this.code.WriteLine("if (cursor.Location + 1 <= cursor.Subject.Length)");
                this.code.WriteLine("{");
                this.code.Indent++;
                this.code.WriteLine("var c = cursor.Subject[cursor.Location];");
                this.code.WriteLine("bool match = false;");
                this.code.WriteLine("for (int i = 0; !match && i < characterRanges.Length; i += 2)");
                this.code.WriteLine("{");
                this.code.Indent++;
                this.code.WriteLine("match = c >= characterRanges[i] && c <= characterRanges[i + 1];");
                this.code.Indent--;
                this.code.WriteLine("}");
                this.code.WriteLine("if (!match && ignoreCase && (char.IsUpper(c) || char.IsLower(c)))");
                this.code.WriteLine("{");
                this.code.Indent++;
                this.code.WriteLine("var cs = c.ToString();");
                this.code.WriteLine("for (int i = 0; !match && i < characterRanges.Length; i += 2)");
                this.code.WriteLine("{");
                this.code.Indent++;
                this.code.WriteLine("var min = characterRanges[i];");
                this.code.WriteLine("var max = characterRanges[i + 1];");
                this.code.WriteLine("for (char o = min; !match && o <= max; o++)");
                this.code.WriteLine("{");
                this.code.Indent++;
                this.code.WriteLine("match = (char.IsUpper(o) || char.IsLower(o)) && cs.Equals(o.ToString(), StringComparison.CurrentCultureIgnoreCase);");
                this.code.Indent--;
                this.code.WriteLine("}");
                this.code.Indent--;
                this.code.WriteLine("}");
                this.code.Indent--;
                this.code.WriteLine("}");
                this.code.WriteLine("if (match ^ negated)");
                this.code.WriteLine("{");
                this.code.Indent++;
                this.code.WriteLine("var result = new ParseResult<string>(1, cursor.Subject.Substring(cursor.Location, 1));");
                this.code.WriteLine("cursor = cursor.Advance(result);");
                this.code.WriteLine("return result;");
                this.code.Indent--;
                this.code.WriteLine("}");
                this.code.Indent--;
                this.code.WriteLine("}");
                this.code.WriteLine("this.ReportError(cursor, \"[\" + (negated ? \"^\" : \"\")  + characterRanges + \"]\");");
                this.code.WriteLine("return null;");
                this.code.Indent--;
                this.code.WriteLine("}");

                this.code.WriteLineNoTabs("");
                this.code.WriteLine("private ParseResult<string> ParseAny(ref Cursor cursor)");
                this.code.WriteLine("{");
                this.code.Indent++;
                this.code.WriteLine("if (cursor.Location + 1 <= cursor.Subject.Length)");
                this.code.WriteLine("{");
                this.code.Indent++;
                this.code.WriteLine("var substr = cursor.Subject.Substring(cursor.Location, 1);");
                this.code.WriteLine("var result = new ParseResult<string>(1, substr);");
                this.code.WriteLine("cursor = cursor.Advance(result);");
                this.code.WriteLine("return result;");
                this.code.Indent--;
                this.code.WriteLine("}");
                this.code.WriteLine("this.ReportError(cursor, \"any character\");");
                this.code.WriteLine("return null;");
                this.code.Indent--;
                this.code.WriteLine("}");

                this.code.WriteLineNoTabs("");
                this.code.WriteLine("private void ReportError(Cursor cursor, string expected)");
                this.code.WriteLine("{");
                this.code.Indent++;
                this.code.WriteLine("if (this.rightmostErrorCursor != null && this.rightmostErrorCursor.Location > cursor.Location)");
                this.code.WriteLine("{");
                this.code.Indent++;
                this.code.WriteLine("return;");
                this.code.Indent--;
                this.code.WriteLine("}");
                this.code.WriteLine("if (this.rightmostErrorCursor == null || this.rightmostErrorCursor.Location < cursor.Location)");
                this.code.WriteLine("{");
                this.code.Indent++;
                this.code.WriteLine("this.rightmostErrorCursor = cursor;");
                this.code.WriteLine("this.rightmostErrors.Clear();");
                this.code.Indent--;
                this.code.WriteLine("}");
                this.code.WriteLine("this.rightmostErrors.Add(expected);");
                this.code.Indent--;
                this.code.WriteLine("}");

                this.code.Indent--;
                this.code.WriteLine("}");

                this.code.Indent--;
                this.code.WriteLine("}");
            }

            protected override void WalkRule(Rule rule)
            {
                this.code.WriteLineNoTabs("");
                this.code.WriteLine("private ParseResult<string> " + EscapeName(rule.Name) + "(ref Cursor cursor)");
                this.code.WriteLine("{");
                this.code.Indent++;

                this.currentResultName = "r" + this.Id;
                this.code.WriteLine("ParseResult<string> " + this.currentResultName + " = null;");
                base.WalkRule(rule);
                this.code.WriteLine("return " + this.currentResultName + ";");
                this.currentResultName = null;

                this.code.Indent--;
                this.code.WriteLine("}");
                this.id = 0;
            }

            protected override void WalkLiteralExpression(LiteralExpression literalExpression)
            {
                this.code.WriteLine(this.currentResultName + " = this.ParseLiteral(ref cursor, " + ToLiteral(literalExpression.Value) + (literalExpression.IgnoreCase ? ", ignoreCase: true" : "") + ");");
            }

            protected override void WalkWildcardExpression(WildcardExpression wildcardExpression)
            {
                this.code.WriteLine(this.currentResultName + " = this.ParseAny(ref cursor);");
            }

            protected override void WalkNameExpression(NameExpression nameExpression)
            {
                this.code.WriteLine(this.currentResultName + " = this." + EscapeName(nameExpression.Name) + "(ref cursor);");
            }

            protected override void WalkClassExpression(ClassExpression classExpression)
            {
                this.code.WriteLine(this.currentResultName + " = this.ParseClass(ref cursor, " + ToLiteral(string.Join(string.Empty, classExpression.Ranges.SelectMany(r => new[] { r.Min, r.Max }))) + (classExpression.Negated ? ", negated: true" : "") + (classExpression.IgnoreCase ? ", ignoreCase: true" : "") + ");");
            }

            protected override void WalkSequenceExpression(SequenceExpression sequenceExpression)
            {
                var startId = this.Id;
                this.code.WriteLine("var startCursor" + startId + " = cursor;");

                var oldResultName = this.currentResultName;

                foreach (var expression in sequenceExpression.Sequence)
                {
                    this.currentResultName = "r" + this.Id;
                    this.code.WriteLine("ParseResult<string> " + this.currentResultName + " = null;");
                    this.WalkExpression(expression);
                    this.code.WriteLine("if (" + this.currentResultName + " != null)");
                    this.code.WriteLine("{");
                    this.code.Indent++;
                }

                this.currentResultName = oldResultName;

                this.code.WriteLine("var len = cursor.Location - startCursor" + startId + ".Location;");
                this.code.WriteLine(this.currentResultName + " = new ParseResult<string>(len, cursor.Subject.Substring(startCursor" + startId + ".Location, len));");

                for (int i = 0; i < sequenceExpression.Sequence.Count; i++)
                {
                    this.code.Indent--;
                    this.code.WriteLine("}");
                    this.code.WriteLine("else");
                    this.code.WriteLine("{");
                    this.code.Indent++;
                    this.code.WriteLine("cursor = startCursor" + startId + ";");
                    this.code.Indent--;
                    this.code.WriteLine("}");
                }
            }

            protected override void WalkChoiceExpression(ChoiceExpression choiceExpression)
            {
                foreach (var expression in choiceExpression.Choices)
                {
                    this.code.WriteLine("if (" + this.currentResultName + " == null)");
                    this.code.WriteLine("{");
                    this.code.Indent++;
                    this.WalkExpression(expression);
                }

                for (int i = 0; i < choiceExpression.Choices.Count; i++)
                {
                    this.code.Indent--;
                    this.code.WriteLine("}");
                }
            }

            protected override void WalkRepetitionExpression(RepetitionExpression repetitionExpression)
            {
                var startId = this.Id;
                this.code.WriteLine("var startCursor" + startId + " = cursor;");

                var listName = "l" + this.Id;
                var oldResultName = this.currentResultName;
                this.currentResultName = "r" + this.Id;

                this.code.WriteLine("var " + listName + " = new List<string>();");
                this.code.WriteLine("while (" + (repetitionExpression.Max.HasValue ? listName + ".Count < " + repetitionExpression.Max : "true") + ")");
                this.code.WriteLine("{");
                this.code.Indent++;
                this.code.WriteLine("ParseResult<string> " + this.currentResultName + " = null;");
                this.WalkExpression(repetitionExpression.Expression);
                this.code.WriteLine("if (" + this.currentResultName + " != null)");
                this.code.WriteLine("{");
                this.code.Indent++;
                this.code.WriteLine(listName + ".Add(" + this.currentResultName + ".Value);");
                this.code.Indent--;
                this.code.WriteLine("}");
                this.code.WriteLine("else");
                this.code.WriteLine("{");
                this.code.Indent++;
                this.code.WriteLine("break;");
                this.code.Indent--;
                this.code.WriteLine("}");
                this.code.Indent--;
                this.code.WriteLine("}");

                this.currentResultName = oldResultName;

                this.code.WriteLine("if (" + listName + ".Count >= " + repetitionExpression.Min + ")");
                this.code.WriteLine("{");
                this.code.Indent++;
                this.code.WriteLine("var len = cursor.Location - startCursor" + startId + ".Location;");
                this.code.WriteLine(this.currentResultName + " = new ParseResult<string>(len, cursor.Subject.Substring(startCursor" + startId + ".Location, len));");
                this.code.Indent--;
                this.code.WriteLine("}");
                this.code.WriteLine("else");
                this.code.WriteLine("{");
                this.code.Indent++;
                this.code.WriteLine("cursor = startCursor" + startId + ";");
                this.code.Indent--;
                this.code.WriteLine("}");
            }

            protected override void WalkAndExpression(AndExpression andExpression)
            {
                WalkAssertionExpression(andExpression.Expression, mustMatch: true);
            }

            protected override void WalkNotExpression(NotExpression notExpression)
            {
                WalkAssertionExpression(notExpression.Expression, mustMatch: false);
            }

            private void WalkAssertionExpression(Expression expression, bool mustMatch)
            {
                var startId = this.Id;
                this.code.WriteLine("var startCursor" + startId + " = cursor;");

                var oldResultName = this.currentResultName;
                this.currentResultName = "r" + this.Id;
                this.code.WriteLine("ParseResult<string> " + this.currentResultName + " = null;");
                this.WalkExpression(expression);

                this.code.WriteLine("cursor = startCursor" + startId + ";");

                this.code.WriteLine("if (" + this.currentResultName + (mustMatch ? "==" : "!=") + "null)");
                this.code.WriteLine("{");
                this.currentResultName = oldResultName;
                this.code.WriteLine(this.currentResultName + " = new ParseResult<string>(0, string.Empty);");
                this.code.WriteLine("}");
            }

            protected override void WalkPrefixedExpression(PrefixedExpression prefixedExpression)
            {
                this.WalkExpression(prefixedExpression.Expression);

                this.code.WriteLine("var " + EscapeName(prefixedExpression.Prefix) + " = " + this.currentResultName + ";");
            }

            private static Dictionary<char, string> simpleEscapeChars = new Dictionary<char, string>()
            {
                { '\'', "\\'" }, { '\"', "\\\"" }, { '\\', "\\\\" }, { '\0', "\\0" },
                { '\a', "\\a" }, { '\b', "\\b" }, { '\f', "\\f" }, { '\n', "\\n" },
                { '\r', "\\r" }, { '\t', "\\t" }, { '\v', "\\v" },
            };

            private static string ToLiteral(string input)
            {
                var sb = new StringBuilder();
                sb.Append("\"");
                for (int i = 0; i < input.Length; i++)
                {
                    var c = input[i];

                    string literal;
                    if (simpleEscapeChars.TryGetValue(c, out literal))
                    {
                        sb.Append(literal);
                    }
                    else if (c >= 32 && c <= 126)
                    {
                        sb.Append(c);
                    }
                    else
                    {
                        sb.Append("\\u").Append(((int)c).ToString("x4"));
                    }
                }
                sb.Append("\"");
                return sb.ToString();
            }
        }
    }
}
