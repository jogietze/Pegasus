﻿// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Pegasus.Tests.Compiler
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using NUnit.Framework;
    using Pegasus.Common;
    using Pegasus.Compiler;
    using Pegasus.Parser;
    using static PerformanceTestUtils;

    public class PegCompilerTests
    {
        [Test]
        [Category("Performance")]
        public void Compile_Performance_PegGrammar()
        {
            var pegGrammar = new PegParser().Parse(File.ReadAllText("PegParser.peg"));

            Evaluate(() =>
            {
                PegCompiler.Compile(pegGrammar);
            });
        }

        [Test]
        public void Compile_WhenACSharpExpressionContainsError_YieldsError()
        {
            var grammar = new PegParser().Parse("a = {{ return \"OK\" }}");

            var result = PegCompiler.Compile(grammar);

            var error = result.Errors.First();
            Assert.That(error.ErrorNumber, Is.EqualTo("CS1002"));
            Assert.That(error.IsWarning, Is.False);
        }

        [Test]
        public void Compile_WhenACSharpExpressionContainsWarnings_YieldsWarning()
        {
            var grammar = new PegParser().Parse("a = {{\n#warning OK\nreturn \"OK\";\n}}");

            var result = PegCompiler.Compile(grammar);

            var error = result.Errors.First();
            Assert.That(error.ErrorNumber, Is.EqualTo("CS1030"));
            Assert.That(error.IsWarning, Is.True);
        }

        [Test]
        public void Compile_WhenExportedRuleNameIsLowercase_YieldsWarning()
        {
            var grammar = new PegParser().Parse("a -export = 'OK'");

            var result = PegCompiler.Compile(grammar);

            var error = result.Errors.First();
            Assert.That(error.ErrorNumber, Is.EqualTo("PEG0025"));
            Assert.That(error.IsWarning, Is.True);
        }

        [Test]
        public void Compile_WhenPublicRuleNameIsLowercase_YieldsWarning()
        {
            var grammar = new PegParser().Parse("a -public = 'OK'");

            var result = PegCompiler.Compile(grammar);

            var error = result.Errors.First();
            Assert.That(error.ErrorNumber, Is.EqualTo("PEG0025"));
            Assert.That(error.IsWarning, Is.True);
        }

        [Test]
        public void Compile_WhenTheGrammarContainsAnAndCodeExpression_ExecutesExpression()
        {
            var grammar = new PegParser().Parse("a = &{true} 'OK'");

            var compiled = PegCompiler.Compile(grammar);
            var parser = CodeCompiler.Compile<string>(compiled);

            Assert.That(parser.Parse("OK"), Is.EqualTo("OK"));
        }

        [Test]
        public void Compile_WhenTheGrammarContainsAnAndExpression_ExecutesExpression()
        {
            var grammar = new PegParser().Parse("a = &other 'OK'; other <int> = 'OK' { 0 }");

            var compiled = PegCompiler.Compile(grammar);
            var parser = CodeCompiler.Compile<string>(compiled);

            Assert.That(parser.Parse("OK"), Is.EqualTo("OK"));
        }

        [TestCase("string")]
        [TestCase("foo")]
        [TestCase("bar")]
        public void Compile_WhenTheGrammarContainsAnAndExpression_TheTypeOfTheAndExpressionReflectsTheInnerExpression(string type)
        {
            var grammar = new PegParser().Parse("a = x:&(<" + type + "> 'OK' { null })");
            var compiled = PegCompiler.Compile(grammar);

            Assert.That(compiled.ExpressionTypes[grammar.Rules.Single().Expression].ToString(), Is.EqualTo(type));
        }

        [Test]
        public void Compile_WhenTheGrammarContainsANotCodeExpression_ExecutesExpression()
        {
            var grammar = new PegParser().Parse("a = !{false} 'OK'");

            var compiled = PegCompiler.Compile(grammar);
            var parser = CodeCompiler.Compile<string>(compiled);

            Assert.That(parser.Parse("OK"), Is.EqualTo("OK"));
        }

        [Test]
        public void Compile_WhenTheGrammarContainsANotExpression_ExecutesExpression()
        {
            var grammar = new PegParser().Parse("a = !other 'OK'; other <int> = 'NO' { 0 }");

            var compiled = PegCompiler.Compile(grammar);
            var parser = CodeCompiler.Compile<string>(compiled);

            Assert.That(parser.Parse("OK"), Is.EqualTo("OK"));
        }

        [Test]
        public void Compile_WhenTheGrammarContainsAParseExpression_ExecutesTheParseExpression()
        {
            var grammar = new PegParser().Parse("a = #parse{ this.ReturnHelper<string>(state, ref state, _ => \"OK\") };");

            var compiled = PegCompiler.Compile(grammar);
            var parser = CodeCompiler.Compile<string>(compiled);

            Assert.That(parser.Parse(string.Empty), Is.EqualTo("OK"));
        }

        [TestCase(true, "&", "OK")]
        [TestCase(false, "&", "")]
        [TestCase(true, "!", "")]
        [TestCase(false, "!", "OK")]
        public void Compile_WhenTheGrammarHasACodeAssertion_RespectsTheReturnValueOfTheExpression(bool expression, string assertion, string expected)
        {
            var grammar = new PegParser().Parse($"a = 'OK' {assertion}{{ {expression.ToString().ToLower()} }} / ;");
            var compiled = PegCompiler.Compile(grammar);
            var parser = CodeCompiler.Compile<string>(compiled);

            var result = parser.Parse("OK");

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void Compile_WhenTheGrammarHasAnErrorExpressionInTheMiddleOfASequence_ThrowsException()
        {
            var grammar = new PegParser().Parse("a = 'OK' #error{ \"OK\" } 'OK'");
            var compiled = PegCompiler.Compile(grammar);
            var parser = CodeCompiler.Compile<string>(compiled);

            Assert.That(() => parser.Parse("OK"), Throws.InnerException.InstanceOf<FormatException>().With.InnerException.Message.EqualTo("OK"));
        }

        [Test]
        public void Compile_WhenTheGrammarHasAnErrorExpressionInTheMiddleOfASequenceWrappedInParentheses_ThrowsException()
        {
            var grammar = new PegParser().Parse("a = 'OK' (#error{ \"OK\" }) 'OK'");
            var compiled = PegCompiler.Compile(grammar);
            var parser = CodeCompiler.Compile<string>(compiled);

            Assert.That(() => parser.Parse("OK"), Throws.InnerException.InstanceOf<FormatException>().With.InnerException.Message.EqualTo("OK"));
        }

        [Test]
        public void Compile_WhenTheGrammarHasAZeroWidthLexicalRule_ProducesAParserThatReturnsTheLexicalElements()
        {
            var grammar = new PegParser().Parse("a = b 'OK'; b -lexical = ;");
            var compiled = PegCompiler.Compile(grammar);
            var parser = CodeCompiler.Compile<string>(compiled);

            var result = parser.Parse("OK", null, out var lexicalElements);

            var actual = lexicalElements.Select(e => e.Name + "@" + e.StartCursor.Location + ":" + e.EndCursor.Location).ToArray();
            Assert.That(actual, Is.EqualTo(new[] { "b@0:0" }));
        }

        [Test]
        public void Compile_WhenTheGrammarHasLexicalRuleConsistingOfAClassExpression_ProducesAParserThatReturnsTheLexicalElements()
        {
            var grammar = new PegParser().Parse("a -lexical = [O];");
            var compiled = PegCompiler.Compile(grammar);
            var parser = CodeCompiler.Compile<string>(compiled);

            var result = parser.Parse("OK", null, out var lexicalElements);

            var actual = lexicalElements.Select(e => e.Name + "@" + e.StartCursor.Location + ":" + e.EndCursor.Location).ToArray();
            Assert.That(actual, Is.EqualTo(new[] { "a@0:1" }));
        }

        [Test]
        public void Compile_WhenTheGrammarHasLexicalRuleConsistingOfALiteralExpression_ProducesAParserThatReturnsTheLexicalElements()
        {
            var grammar = new PegParser().Parse("a -lexical = 'OK';");
            var compiled = PegCompiler.Compile(grammar);
            var parser = CodeCompiler.Compile<string>(compiled);

            var result = parser.Parse("OK", null, out var lexicalElements);

            var actual = lexicalElements.Select(e => e.Name + "@" + e.StartCursor.Location + ":" + e.EndCursor.Location).ToArray();
            Assert.That(actual, Is.EqualTo(new[] { "a@0:2" }));
        }

        [Test]
        public void Compile_WhenTheGrammarHasLexicalRuleConsistingOfANameExpression_ProducesAParserThatReturnsTheLexicalElements()
        {
            var grammar = new PegParser().Parse("a -lexical = b; b = 'OK';");
            var compiled = PegCompiler.Compile(grammar);
            var parser = CodeCompiler.Compile<string>(compiled);

            var result = parser.Parse("OK", null, out var lexicalElements);

            var actual = lexicalElements.Select(e => e.Name + "@" + e.StartCursor.Location + ":" + e.EndCursor.Location).ToArray();
            Assert.That(actual, Is.EqualTo(new[] { "a@0:2" }));
        }

        [Test]
        public void Compile_WhenTheGrammarHasLexicalRuleConsistingOfARepetitionExpression_ProducesAParserThatReturnsTheLexicalElements()
        {
            var grammar = new PegParser().Parse("a -lexical = [OK]+;");
            var compiled = PegCompiler.Compile(grammar);
            var parser = CodeCompiler.Compile<IList<string>>(compiled);

            var result = parser.Parse("OK", null, out var lexicalElements);

            var actual = lexicalElements.Select(e => e.Name + "@" + e.StartCursor.Location + ":" + e.EndCursor.Location).ToArray();
            Assert.That(actual, Is.EqualTo(new[] { "a@0:2" }));
        }

        [Test]
        public void Compile_WhenTheGrammarHasLexicalRuleConsistingOfAStateExpression_ProducesAParserThatReturnsTheLexicalElements()
        {
            var grammar = new PegParser().Parse("a -lexical = #{};");
            var compiled = PegCompiler.Compile(grammar);
            var parser = CodeCompiler.Compile<string>(compiled);

            var result = parser.Parse("OK", null, out var lexicalElements);

            var actual = lexicalElements.Select(e => e.Name + "@" + e.StartCursor.Location + ":" + e.EndCursor.Location).ToArray();
            Assert.That(actual, Is.EqualTo(new[] { "a@0:0" }));
        }

        [Test]
        public void Compile_WhenTheGrammarHasLexicalRuleConsistingOfAWildcardExpression_ProducesAParserThatReturnsTheLexicalElements()
        {
            var grammar = new PegParser().Parse("a -lexical = .;");
            var compiled = PegCompiler.Compile(grammar);
            var parser = CodeCompiler.Compile<string>(compiled);

            var result = parser.Parse("OK", null, out var lexicalElements);

            var actual = lexicalElements.Select(e => e.Name + "@" + e.StartCursor.Location + ":" + e.EndCursor.Location).ToArray();
            Assert.That(actual, Is.EqualTo(new[] { "a@0:1" }));
        }

        [Test]
        public void Compile_WhenTheGrammarHasResourceStringWithoutResourcesSpecified_YieldsError()
        {
            var grammar = new PegParser().Parse("a = 'OkResource'r");

            var result = PegCompiler.Compile(grammar);

            var error = result.Errors.Single();
            Assert.That(error.ErrorNumber, Is.EqualTo("PEG0016"));
            Assert.That(error.IsWarning, Is.False);
        }

        [Test]
        public void Compile_WhenTheGrammarHasTwoLexicalRulesThatBeginAndEndOnTheSameCharacter_ProducesAParserThatReturnsBothLexicalElements()
        {
            var grammar = new PegParser().Parse("a -lexical = b; b -lexical = 'OK';");
            var compiled = PegCompiler.Compile(grammar);
            var parser = CodeCompiler.Compile<string>(compiled);

            var result = parser.Parse("OK", null, out var lexicalElements);

            var actual = lexicalElements.Select(e => e.Name + "@" + e.StartCursor.Location + ":" + e.EndCursor.Location).ToArray();
            Assert.That(actual, Is.EqualTo(new[] { "b@0:2", "a@0:2" }));
        }

        [Test]
        public void Compile_WhenTheGrammarHasTwoLexicalRulesThatBeginOnTheSameCharacter_ProducesAParserThatReturnsBothLexicalElements()
        {
            var grammar = new PegParser().Parse("a -lexical = b ' '; b -lexical = 'OK';");
            var compiled = PegCompiler.Compile(grammar);
            var parser = CodeCompiler.Compile<string>(compiled);

            var result = parser.Parse("OK ", null, out var lexicalElements);

            var actual = lexicalElements.Select(e => e.Name + "@" + e.StartCursor.Location + ":" + e.EndCursor.Location).ToArray();
            Assert.That(actual, Is.EqualTo(new[] { "b@0:2", "a@0:3" }));
        }

        [Test]
        public void Compile_WhenTheGrammarHasTwoLexicalRulesThatEndOnTheSameCharacter_ProducesAParserThatReturnsBothLexicalElements()
        {
            var grammar = new PegParser().Parse("a -lexical = ' ' b; b -lexical = 'OK';");
            var compiled = PegCompiler.Compile(grammar);
            var parser = CodeCompiler.Compile<string>(compiled);

            var result = parser.Parse(" OK", null, out var lexicalElements);

            var actual = lexicalElements.Select(e => e.Name + "@" + e.StartCursor.Location + ":" + e.EndCursor.Location).ToArray();
            Assert.That(actual, Is.EqualTo(new[] { "b@1:3", "a@0:3" }));
        }

        [Test]
        public void Compile_WhenTheGrammarHasTwoNestedLexicalRules_ProducesAParserThatReturnsBothLexicalElements()
        {
            var grammar = new PegParser().Parse("a -lexical = ' ' b ' '; b -lexical = 'OK';");
            var compiled = PegCompiler.Compile(grammar);
            var parser = CodeCompiler.Compile<string>(compiled);

            var result = parser.Parse(" OK ", null, out var lexicalElements);

            var actual = lexicalElements.Select(e => e.Name + "@" + e.StartCursor.Location + ":" + e.EndCursor.Location).ToArray();
            Assert.That(actual, Is.EqualTo(new[] { "b@1:3", "a@0:4" }));
        }

        [Test]
        public void Compile_WhenTheGrammarRepeatsALabel_YieldsError()
        {
            var grammar = new PegParser().Parse("a = foo:'OK' foo:'OK'");

            var result = PegCompiler.Compile(grammar);

            var error = result.Errors.Single();
            Assert.That(error.ErrorNumber, Is.EqualTo("PEG0007"));
            Assert.That(error.IsWarning, Is.False);
        }

        [TestCase("a = (#parse{ this.b(ref state) })* b; b = 'OK';", "PEG0021")]
        [TestCase("a = (#parse{ this.b(ref state) })<1,5> b; b = 'OK';", "PEG0022")]
        public void Compile_WhenTheGrammarRepeatsAParseCodeExpressionWithNoMaximum_YieldsWarning(string subject, string errorNumber)
        {
            var grammar = new PegParser().Parse(subject);

            var result = PegCompiler.Compile(grammar);

            var error = result.Errors.First();
            Assert.That(error.ErrorNumber, Is.EqualTo(errorNumber));
            Assert.That(error.IsWarning, Is.True);
        }

        [Test]
        public void Compile_WhenTheGrammarRepeatsAZeroWidthExpressionContainingAnAssertionWithFiniteMaximum_YieldsWarning()
        {
            var grammar = new PegParser().Parse("a = (&{false} '')<1,5>");

            var result = PegCompiler.Compile(grammar);

            var error = result.Errors.First();
            Assert.That(error.ErrorNumber, Is.EqualTo("PEG0022"));
            Assert.That(error.IsWarning, Is.True);
        }

        [Test]
        public void Compile_WhenTheGrammarRepeatsAZeroWidthExpressionContainingAnAssertionWithNoMaximum_YieldsWarning()
        {
            var grammar = new PegParser().Parse("a = (&{false} '')*");

            var result = PegCompiler.Compile(grammar);

            var error = result.Errors.First();
            Assert.That(error.ErrorNumber, Is.EqualTo("PEG0021"));
            Assert.That(error.IsWarning, Is.True);
        }

        [Test]
        public void Compile_WhenTheGrammarRepeatsAZeroWidthExpressionContainingAnAssertionWithSameMinAndMax_YieldsNone()
        {
            var grammar = new PegParser().Parse("a = (&{false} '')<5>");

            var result = PegCompiler.Compile(grammar);

            Assert.That(result.Errors, Is.Empty);
        }

        [Test]
        public void Compile_WhenTheGrammarRepeatsAZeroWidthExpressionWithFiniteMaximum_YieldsWarning()
        {
            var grammar = new PegParser().Parse("a = ''<1,5>");

            var result = PegCompiler.Compile(grammar);

            var error = result.Errors.First();
            Assert.That(error.ErrorNumber, Is.EqualTo("PEG0022"));
            Assert.That(error.IsWarning, Is.True);
        }

        [Test]
        public void Compile_WhenTheGrammarRepeatsAZeroWidthExpressionWithNoMaximum_YieldsError()
        {
            var grammar = new PegParser().Parse("a = ''*");

            var result = PegCompiler.Compile(grammar);

            var error = result.Errors.First();
            Assert.That(error.ErrorNumber, Is.EqualTo("PEG0021"));
            Assert.That(error.IsWarning, Is.False);
        }

        [Test]
        public void Compile_WhenTheGrammarRepeatsAZeroWidthExpressionWithSameMinAndMax_YieldsNone()
        {
            var grammar = new PegParser().Parse("a = ''<5>");

            var result = PegCompiler.Compile(grammar);

            Assert.That(result.Errors, Is.Empty);
        }

        [Test]
        public void Compile_WhenTheGrammarUsesAnUnknownFlag_YieldsWarning()
        {
            var grammar = new PegParser().Parse("a -unknown = 'OK'");

            var result = PegCompiler.Compile(grammar);

            var error = result.Errors.Single();
            Assert.That(error.ErrorNumber, Is.EqualTo("PEG0013"));
            Assert.That(error.IsWarning, Is.True);
        }

        [Test]
        public void Compile_WhenTheResultOfAnAndExpressionIsReturned_ReturnsTheResultOfTheAndExpression([Range(0, 9)] int value)
        {
            var grammar = new PegParser().Parse("a <int> = x:&(<int> d:. { int.Parse(d) }) { x }");
            var compiled = PegCompiler.Compile(grammar);
            var parser = CodeCompiler.Compile<int>(compiled);

            var result = parser.Parse(value.ToString());

            Assert.That(result, Is.EqualTo(value));
        }

        [TestCase("a = b; b = c; c = d; d = a;")]
        [TestCase("a = b / c; b = 'OK'; c = a;")]
        [TestCase("a = &b c; b = a; c = 'OK';")]
        public void Compile_WithAmbiguousLeftRecursion_YieldsError(string subject)
        {
            var parser = new PegParser();
            var grammar = parser.Parse(subject);

            var result = PegCompiler.Compile(grammar);

            var error = result.Errors.First();
            Assert.That(error.ErrorNumber, Is.EqualTo("PEG0023"));
            Assert.That(error.IsWarning, Is.False);
        }

        [TestCase(0, 0)]
        [TestCase(1, 0)]
        [TestCase(2, 1)]
        public void Compile_WithAnImpossibleQuantifier_YieldsWarning(int min, int max)
        {
            var grammar = new PegParser().Parse("a = 'OK'<" + min + "," + max + ">;");

            var result = PegCompiler.Compile(grammar);

            var error = result.Errors.First();
            Assert.That(error.ErrorNumber, Is.EqualTo("PEG0015"));
            Assert.That(error.IsWarning, Is.True);
        }

        [TestCase("a = 'Ok';", "Ok", "OK")]
        [TestCase("@ignorecase false; a = 'Ok';", "Ok", "OK")]
        [TestCase("@ignorecase true; a = 'Ok';", "OK", "XX")]
        [TestCase("a = [O] [k];", "Ok", "OK")]
        [TestCase("@ignorecase false; a = [O] [k];", "Ok", "OK")]
        [TestCase("@ignorecase true; a = [O] [k];", "OK", "XX")]
        [TestCase("a = 'Ok'i;", "OK", "XX")]
        [TestCase("@ignorecase false; a = 'Ok'i;", "OK", "XX")]
        [TestCase("@ignorecase true; a = 'Ok'i;", "OK", "XX")]
        [TestCase("a = [O]i [k]i;", "OK", "XX")]
        [TestCase("@ignorecase false; a = [O]i [k]i;", "OK", "XX")]
        [TestCase("@ignorecase true; a = [O]i [k]i;", "OK", "XX")]
        [TestCase("a = 'Ok's;", "Ok", "OK")]
        [TestCase("@ignorecase false; a = 'Ok's;", "Ok", "OK")]
        [TestCase("@ignorecase true; a = 'Ok's;", "Ok", "OK")]
        [TestCase("a = [O]s [k]s;", "Ok", "OK")]
        [TestCase("@ignorecase false; a = [O]s [k]s;", "Ok", "OK")]
        [TestCase("@ignorecase true; a = [O]s [k]s;", "Ok", "OK")]
        public void Compile_WithCaseSensitivityCombinations_ProducesCorrectParser(string subject, string match, string unmatch)
        {
            var grammar = new PegParser().Parse(subject);
            var compiled = PegCompiler.Compile(grammar);
            var parser = CodeCompiler.Compile<string>(compiled);

            Assert.That(parser.Parse(match), Is.EqualTo(match));
            Assert.That(() => parser.Parse(unmatch), Throws.Exception);
        }

        [Test]
        public void Compile_WithComplexLeftRecursion_Succeeds()
        {
            var parser = new PegParser();
            var grammar = parser.Parse(@"a<o> -memoize = b;
                                         b<o> -memoize = c;
                                         c<o> -memoize = e / d;
                                         d<o> = e;
                                         e<o> -memoize = f;
                                         f<o> -memoize = g;
                                         g<o> -memoize = g;");

            var result = PegCompiler.Compile(grammar);

            Assert.That(result.Errors, Is.Empty);
        }

        [Test]
        public void Compile_WithDuplicateDefinition_YieldsError()
        {
            var grammar = new PegParser().Parse("a = 'a'; a = 'b';");

            var result = PegCompiler.Compile(grammar);

            var error = result.Errors.Single();
            Assert.That(error.ErrorNumber, Is.EqualTo("PEG0002"));
            Assert.That(error.IsWarning, Is.False);
        }

        [TestCase("namespace")]
        [TestCase("classname")]
        public void Compile_WithDuplicateSetting_YieldsError(string settingName)
        {
            var grammar = new PegParser().Parse("@" + settingName + " OK; @" + settingName + " OK; a = 'OK';");

            var result = PegCompiler.Compile(grammar);

            var error = result.Errors.Single();
            Assert.That(error.ErrorNumber, Is.EqualTo("PEG0005"));
            Assert.That(error.IsWarning, Is.False);
        }

        [Test]
        public void Compile_WithExpressionWhoseTypeIsNotDefined_YieldsError()
        {
            var parser = new PegParser();
            var grammar = parser.Parse("a -memoize = a;");

            var result = PegCompiler.Compile(grammar);

            var error = result.Errors.Single();
            Assert.That(error.ErrorNumber, Is.EqualTo("PEG0019"));
            Assert.That(error.IsWarning, Is.False);
        }

        [TestCase("accessibility", "private")]
        public void Compile_WithInvalidSettingValue_YieldsError(string settingName, string value)
        {
            var grammar = new PegParser().Parse("@" + settingName + " {" + value + "}; a = 'OK';");

            var result = PegCompiler.Compile(grammar);

            var error = result.Errors.Single();
            Assert.That(error.ErrorNumber, Is.EqualTo("PEG0012"));
            Assert.That(error.IsWarning, Is.False);
        }

        [Test]
        public void Compile_WithMissingRuleDefinition_YieldsError()
        {
            var grammar = new PegParser().Parse("a = b");

            var result = PegCompiler.Compile(grammar);

            var error = result.Errors.Single();
            Assert.That(error.ErrorNumber, Is.EqualTo("PEG0003"));
            Assert.That(error.IsWarning, Is.False);
        }

        [Test]
        public void Compile_WithMissingStartRule_YieldsError()
        {
            var grammar = new PegParser().Parse("@start b; a = 'OK'");

            var result = PegCompiler.Compile(grammar);

            var error = result.Errors.Single();
            Assert.That(error.ErrorNumber, Is.EqualTo("PEG0003"));
            Assert.That(error.IsWarning, Is.False);
        }

        [Test]
        public void Compile_WithNoRules_YieldsError()
        {
            var grammar = new PegParser().Parse(" ");

            var result = PegCompiler.Compile(grammar);

            var error = result.Errors.Single();
            Assert.That(error.ErrorNumber, Is.EqualTo("PEG0001"));
            Assert.That(error.IsWarning, Is.False);
        }

        [TestCase("-export")]
        [TestCase("-public")]
        public void Compile_WithPublicOrExportedRulesWithLowercaseName_YieldsWarning(string flag)
        {
            var grammar = new PegParser().Parse($"start = 'OK'; b {flag} = 'OK';");

            var result = PegCompiler.Compile(grammar);

            var error = result.Errors.Single();
            Assert.That(error.ErrorNumber, Is.EqualTo("PEG0025"));
            Assert.That(error.IsWarning, Is.True);
        }

        [Test]
        public void Compile_WithRulesOnlyUsedByExportOrPublic_YieldsNone()
        {
            var grammar = new PegParser().Parse("start = 'OK'; B -export = 'OK'; C -public = 'OK';");

            var result = PegCompiler.Compile(grammar);

            Assert.That(result.Errors, Is.Empty);
        }

        [Test]
        public void Compile_WithSimpleLeftRecursion_ProducesCorrectParser()
        {
            var grammar = new PegParser().Parse("a <int> -memoize = a:a '+' b:b { a + b } / b; b <int> = c:[0-9] { int.Parse(c) };");

            var compiled = PegCompiler.Compile(grammar);
            var parser = CodeCompiler.Compile<int>(compiled);

            Assert.That(parser.Parse("1+3"), Is.EqualTo(4));
        }

        [Test]
        public void Compile_WithSingleSimpleRule_Succeeds()
        {
            var grammar = new PegParser().Parse("start = 'OK'");

            var result = PegCompiler.Compile(grammar);
            Assert.That(result.Errors, Is.Empty);
        }

        [Test]
        public void Compile_WithStartRule_ProducesCorrectParser()
        {
            var grammar = new PegParser().Parse("@start b; a = #error{ \"wrong start rule\" }; b = 'OK';");

            var compiled = PegCompiler.Compile(grammar);
            var parser = CodeCompiler.Compile<string>(compiled);

            Assert.That(parser.Parse("OK"), Is.EqualTo("OK"));
        }

        [TestCase("a = a;")]
        [TestCase("a = '' a;")]
        [TestCase("a = b a; b = '';")]
        [TestCase("a = ('OK' / '') a;")]
        [TestCase("a = !b a; b = 'OK';")]
        [TestCase("a = b* a; b = 'OK';")]
        [TestCase("a = ''<2,> a;")]
        public void Compile_WithUnmemoizedLeftRecursion_YieldsError(string subject)
        {
            var parser = new PegParser();
            var grammar = parser.Parse(subject);

            var result = PegCompiler.Compile(grammar);

            var error = result.Errors.First();
            Assert.That(error.ErrorNumber, Is.EqualTo("PEG0020"));
            Assert.That(error.IsWarning, Is.False);
        }

        [Test]
        public void Compile_WithUnrecognizedSetting_YieldsWarning()
        {
            var grammar = new PegParser().Parse("@barnacle OK; a = 'OK';");

            var result = PegCompiler.Compile(grammar);

            var error = result.Errors.First();
            Assert.That(error.ErrorNumber, Is.EqualTo("PEG0006"));
            Assert.That(error.IsWarning, Is.True);
        }

        [Test]
        public void Compile_WithUnusedRules_YieldsWarning()
        {
            var grammar = new PegParser().Parse("a = b; b = 'OK'; c = d; d = 'OK' c;");

            var result = PegCompiler.Compile(grammar);

            var error = result.Errors.First();
            Assert.That(error.ErrorNumber, Is.EqualTo("PEG0017"));
            Assert.That(error.IsWarning, Is.True);
        }

        [Test]
        [Category("Performance")]
        [TestCase("simple")]
        [TestCase("gitter-piratejon")]
        public void GeneratedParser_Performance_Regression(string testName)
        {
            var parserSource = File.ReadAllText($@"TestCases\{testName}.peg");
            var subject = File.ReadAllText($@"TestCases\{testName}.txt");
            var parsed = new PegParser().Parse(parserSource);
            var compiled = PegCompiler.Compile(parsed);
            Assert.That(compiled.Errors.Where(e => !e.IsWarning), Is.Empty);
            var pegParser = CodeCompiler.Compile<dynamic>(compiled);

            Evaluate(() =>
            {
                pegParser.Parse(subject);
            });
        }
    }
}
