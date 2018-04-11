﻿/*
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
using System.Linq;
using System.Text;

namespace AnitomySharp
{
  /// <summary>
  /// A class that will tokenize an anime filename.
  /// </summary>
  public class Tokenizer
  {
    private readonly string _filename;
    private readonly List<Element> _elements;
    private readonly Options _options;
    private readonly List<Token> _tokens;
    private static readonly List<Tuple<string, string>> Brackets = new List<Tuple<string, string>>
    {
      new Tuple<string, string>("(", ")"), // U+0028-U+0029
      new Tuple<string, string>("[", "]"), // U+005B-U+005D Square bracket
      new Tuple<string, string>("{", "}"), // U+007B-U+007D Curly bracket
      new Tuple<string, string>("\u300C", "\u300D"),  // Corner bracket
      new Tuple<string, string>("\u300E", "\u300E"),  // White corner bracket
      new Tuple<string, string>("\u3010", "\u3011"), // Black lenticular bracket
      new Tuple<string, string>("\uFF08", "\uFF09") // Fullwidth parenthesis
    };

    /// <summary>
    /// Tokenize a filename into <see cref="Element"/>s
    /// </summary>
    /// <param name="filename">the filename</param>
    /// <param name="elements">the list of elements where pre-identified tokens will be added</param>
    /// <param name="options">the parser options</param>
    /// <param name="tokens">the list of tokens where tokens will be added</param>
    public Tokenizer(string filename, List<Element> elements, Options options, List<Token> tokens)
    {
      if (filename != null) _filename = filename;
      if (elements != null) _elements = elements;
      if (options != null) _options = options;
      if (tokens != null) _tokens = tokens;
    }

    /// <summary>
    /// Returns true if tokenization was successful; false otherwise.
    /// </summary>
    /// <returns></returns>
    public bool Tokenize()
    {
      TokenizeByBrackets();
      return _tokens.Count > 0;
    }

    /// <summary>
    /// Adds a token to the inernal list of tokens
    /// </summary>
    /// <param name="category">the token category</param>
    /// <param name="enclosed">whether or not the token is enclosed in braces</param>
    /// <param name="range">the token range</param>
    private void AddToken(Token.TokenCategory category, bool enclosed, TokenRange range)
    {
//      if (range.Size + range.Offset > _filename.Length) range.Size = _filename.Length - range.Offset;
//      _tokens.Add(new Token(category, _filename.Substring(range.Offset, range.Size), enclosed));
      _tokens.Add(new Token(category, StringHelper.SubstringWithCheck(_filename, range.Offset, range.Size), enclosed));
    }

    private string GetDelimiters(TokenRange range)
    {
      var delimiters = new StringBuilder();

      bool IsDelimiter(char c)
      {
        if (StringHelper.IsAlphanumericChar(c)) return false;
        return _options.AllowedDelimiters.Contains(c.ToString()) && !delimiters.ToString().Contains(c.ToString());
      }

      foreach (var i in Enumerable.Range(range.Offset, Math.Min(_filename.Length, range.Offset + range.Size) - range.Offset)
        .Where(value => IsDelimiter(_filename[value])))
      {
        delimiters.Append(_filename[i]);
      }

//      for (var i = range.Offset; i < Math.Min(_filename.Length, range.Offset + range.Size); i++)
//      {
//        if (IsDelimiter(_filename[i]))
//        {
//          delimiters.Append(_filename[i]);
//        }
//      }

      return delimiters.ToString();
    }

    /// <summary>
    /// Tokenize by bracket.
    /// </summary>
    private void TokenizeByBrackets()
    {
      string matchingBracket = null;

      Func<int, int, int> findFirstBracket = (start, end) =>
      {
        for (var i = start; i < end; i++)
        {
          foreach (var bracket in Brackets)
          {
            if (_filename[i].Equals(char.Parse(bracket.Item1)))
            {
              matchingBracket = bracket.Item2;
              return i;
            }
          }
        }

        return -1;
      };

      var isBracketOpen = false;
      for (var i = 0; i < _filename.Length; )
      {
        int foundIdx;
        if (!isBracketOpen)
        {
          // Look for opening brace
          foundIdx = findFirstBracket(i, _filename.Length);
        }
        else
        {
          // Look for closing brace
          foundIdx = _filename.IndexOf(matchingBracket, i);
        }

        var range = new TokenRange(i, foundIdx == -1 ? _filename.Length : foundIdx - i);
        if (range.Size > 0)
        {
          // Check if our range contains any known anime identifiers
          TokenizeByPreidentified(isBracketOpen, range);
        }

        if (foundIdx != -1)
        {
          // mark as bracket
          AddToken(Token.TokenCategory.Bracket, true, new TokenRange(range.Offset + range.Size, 1));
          isBracketOpen = !isBracketOpen;
          i = foundIdx + 1;
        }
        else
        {
          break;
        }
      }
    }

    /// <summary>
    /// Tokenize by looking for known anime identifiers
    /// </summary>
    /// <param name="enclosed">whether or not the current <code>range</code> is enclosed in braces</param>
    /// <param name="range">the token range</param>
    private void TokenizeByPreidentified(bool enclosed, TokenRange range)
    {
      List<TokenRange> preidentifiedTokens = new List<TokenRange>();

      // Find known anime identifiers
      KeywordManager.Instance.PeekAndAdd(_filename, range, _elements, preidentifiedTokens);

      var offset = range.Offset;
      TokenRange subRange = new TokenRange(range.Offset, 0);
      while (offset < range.Offset + range.Size)
      {
        foreach (var preidentifiedToken in preidentifiedTokens)
        {
          if (offset == preidentifiedToken.Offset)
          {
            if (subRange.Size > 0)
            {
              TokenizeByDelimiters(enclosed, subRange);
            }

            AddToken(Token.TokenCategory.Identifier, enclosed, preidentifiedToken);
            subRange.Offset = preidentifiedToken.Offset + preidentifiedToken.Size;
            offset = subRange.Offset - 1; // It's going to be incremented below
          }
        }

        subRange.Size = ++offset - subRange.Offset;
      }

      // Either there was no preidentified token range, or we're now about to process the tail of our current range
      if (subRange.Size > 0)
      {
        TokenizeByDelimiters(enclosed, subRange);
      }
    }

    /// <summary>
    /// Tokenize by delimiters allowed in <see cref="Options"/>.AllowedDelimiters.
    /// </summary>
    /// <param name="enclosed">whether or not the current <code>range</code> is enclosed in braces</param>
    /// <param name="range">the token range</param>
    private void TokenizeByDelimiters(bool enclosed, TokenRange range)
    {
      var delimiters = GetDelimiters(range);

      if (string.IsNullOrEmpty(delimiters))
      {
        AddToken(Token.TokenCategory.Unknown, enclosed, range);
        return;
      }

      for (int i = range.Offset, end = range.Offset + range.Size; i < end;)
      {
        var found = Enumerable.Range(i, Math.Min(end, _filename.Length) - i)
          .Where(c => delimiters.Contains(_filename[c].ToString()))
          .DefaultIfEmpty(end)
          .FirstOrDefault();
//        int found = end;
//        for (var j = i; j < Math.Min(end, _filename.Length); j++)
//        {
//          if (delimiters.Contains(_filename[j].ToString()))
//          {
//            found = j;
//            break;
//          }
//        }

        TokenRange subRange = new TokenRange(i, found - i);
        if (subRange.Size > 0)
        {
          AddToken(Token.TokenCategory.Unknown, enclosed, subRange);
        }

        if (found != end)
        {
          AddToken(Token.TokenCategory.Delimiter, enclosed, new TokenRange(subRange.Offset + subRange.Size, 1));
          i = found + 1;
        }
        else
        {
          break;
        }
      }

      ValidateDelimiterTokens();
    }

    /// <summary>
    /// Validates tokens (make sure certain words delimited by certain tokens aren't split)
    /// </summary>
    private void ValidateDelimiterTokens()
    {
      Func<Result, bool> isDelimiterToken = r =>
      {
        return r != null && r.Token != null && r.Token.Category == Token.TokenCategory.Delimiter;
      };

      Func<Result, bool> isUnknownToken = r =>
      {
        return r != null && r.Token != null && r.Token.Category == Token.TokenCategory.Unknown;
      };

      Func<Result, bool> isSingleCharacterToken = r =>
      {
        return isUnknownToken(r) && r.Token.Content.Length == 1 && !r.Token.Content.Equals("-");
      };

      Action<Token, Result> appendTokenTo = (src, dest) =>
      {
        dest.Token.Content = dest.Token.Content + src.Content;
        src.Category = Token.TokenCategory.Invalid;
      };

      for (var i = 0; i < _tokens.Count; i++)
      {
        Token token = _tokens[i];
        if (token.Category != Token.TokenCategory.Delimiter) continue;
        char delimiter = token.Content[0];

        Result prevToken = Token.FindPrevToken(_tokens, i, Token.TokenFlag.FlagValid);
        Result nextToken = Token.FindNextToken(_tokens, i, Token.TokenFlag.FlagValid);

        // Check for single-character tokens to prevent splitting group names,
        // keywords, episode numbers, etc.
        if (delimiter != ' ' && delimiter != '_')
        {

          // Single character token
          if (isSingleCharacterToken(prevToken))
          {
            appendTokenTo(token, prevToken);

            while (isUnknownToken(nextToken))
            {
              appendTokenTo(nextToken.Token, prevToken);

              nextToken = Token.FindNextToken(_tokens, i, Token.TokenFlag.FlagValid);
              if (isDelimiterToken(nextToken) && nextToken.Token.Content[0] == delimiter)
              {
                appendTokenTo(nextToken.Token, prevToken);
                nextToken = Token.FindNextToken(_tokens, nextToken, Token.TokenFlag.FlagValid);
              }
            }

            continue;
          }

          if (isSingleCharacterToken(nextToken))
          {
            appendTokenTo(token, prevToken);
            appendTokenTo(nextToken.Token, prevToken);
            continue;
          }
        }

        // Check for adjacent delimiters
        if (isUnknownToken(prevToken) && isDelimiterToken(nextToken))
        {
          var nextDelimiter = nextToken.Token.Content[0];
          if (delimiter != nextDelimiter && delimiter != ',')
          {
            if (nextDelimiter == ' ' || nextDelimiter == '_')
            {
              appendTokenTo(token, prevToken);
            }
          }
        }
      }

      // Remove invalid tokens
      _tokens.RemoveAll(token => token.Category == Token.TokenCategory.Invalid);
    }
  }
}  
