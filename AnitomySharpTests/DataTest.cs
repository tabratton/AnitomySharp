/*
 * Copyright (c) 2014-2017, Eren Okka
 * Copyright (c) 2016-2017, Paul Miller
 * Copyright (c) 2017-2018, Tyler Bratton
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AnitomySharpTests
{
  [TestClass]
  public class DataTest
  {
    [TestMethod]
    public void ValidateParsingResults()
    {
      var path = AppDomain.CurrentDomain.BaseDirectory + @"test-cases.json";
      var testCases = JsonConvert.DeserializeObject<List<TestCase>>(File.ReadAllText(path));

      Console.WriteLine($@"Loaded {testCases.Count} test cases.");
      var stopWatch = new Stopwatch();
      stopWatch.Start();
      foreach (var testCase in testCases)
      {
        Verify(testCase);
      }

      Console.WriteLine($@"Tests took: {stopWatch.ElapsedMilliseconds}(ms)");
      stopWatch.Stop();
    }

    private static void Verify(TestCase entry)
    {
      var fileName = entry.FileName;
      var ignore = entry.Ignore;
      var testCases = entry.Results;

      if (ignore || string.IsNullOrWhiteSpace(fileName) || testCases.Count == 0)
      {
        Console.WriteLine($@"Ignoring [{fileName}] : {{ results: {testCases.Count} | explicit: {ignore}}}");
        return;
      }

      Console.WriteLine($@"Parsing: {fileName}");
      var parseResults = ToTestCaseDict(fileName);

      foreach (var testCase in testCases)
      {
        object testValue;
        if (testCase.Value is JArray)
        {
          var temp = (JArray) testCase.Value;
          testValue = temp.ToObject<List<object>>();
        }
        else
        {
          testValue = testCase.Value;
        }


        ((Dictionary<string, object>) parseResults["results"]).TryGetValue(testCase.Key, out var elValue);
        switch (elValue)
        {
          case null:
            throw new Exception($@"[{fileName}] Missing Element: {testCase.Key} [{testCase.Value}]");
          case string _ when !elValue.Equals(testValue):
            throw new Exception($@"[{fileName}] Incorrect Value: ({testCase.Key}) [{elValue}] {{ required: [{testCase.Value}] }}");
          case IEnumerable<object> _ when !((IEnumerable<object>) testValue).All(v => ((IEnumerable<object>) elValue).Contains(v)):
            throw new Exception($@"[{fileName}] Incorrect List Values:({testCase.Key}) [{elValue}] {{ required: [{testCase.Value}] }}");
        }
      }

    }

    private static Dictionary<string, object> ToTestCaseDict(string filename)
    {
      var parseResults = AnitomySharp.AnitomySharp.Parse(filename);
      var result = new Dictionary<string, object>();
      var elements = new Dictionary<string, object>();

      foreach (var e in parseResults)
      {
        elements.TryGetValue(e.Category.ToString(), out var o);
        if (o != null && o is List<object>)
        {
          ((List<object>) o).Add(e.Value);
        }
        else if (o != null)
        {
          elements[e.Category.ToString()] = new List<object> { o, e.Value };
        }
        else
        {
          elements.Add(e.Category.ToString(), e.Value);
        }
      }

      result.Add("file_name", filename);
      result.Add("results", elements);
      return result;
    }
  }

  public class TestCase
  {
    [JsonProperty("file_name")]
    public string FileName { get; set; }

    [JsonProperty("ignore")]
    public bool Ignore { get; set; }

    [JsonProperty("results")]
    public Dictionary<string, object> Results { get; set; }
  }
}
