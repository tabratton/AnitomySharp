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
using System.Linq;

namespace AnitomySharp
{

  /// <summary>
  /// An anime filename is tokenized into individual <see cref="Token"/>s. This class represents an individual token.
  /// </summary>
  public class Token
  {
    /// <summary>
    /// The category of the token.
    /// </summary>
    public enum TokenCategory
    {
      Unknown,
      Bracket,
      Delimiter,
      Identifier,
      Invalid
    }

    /// <summary>
    /// TokenFlag, used for searching specific token categories. This allows granular searching of TokenCategories.
    /// </summary>
    public enum TokenFlag
    {
      // None
      FlagNone,

      // Categories
      FlagBracket, FlagNotBracket,
      FlagDelimiter, FlagNotDelimiter,
      FlagIdentifier, FlagNotIdentifier,
      FlagUnknown, FlagNotUnknown,
      FlagValid, FlagNotValid,

      // Enclosed (Meaning that it is enclosed in some bracket (e.g. [ ] ))
      FlagEnclosed, FlagNotEnclosed,
    }

    /// <summary>
    /// Set of token category flags
    /// </summary>
    private static readonly List<TokenFlag> FlagMaskCategories = new List<TokenFlag>
    {
      TokenFlag.FlagBracket, TokenFlag.FlagNotBracket,
      TokenFlag.FlagDelimiter, TokenFlag.FlagNotDelimiter,
      TokenFlag.FlagIdentifier, TokenFlag.FlagNotIdentifier,
      TokenFlag.FlagUnknown, TokenFlag.FlagNotUnknown,
      TokenFlag.FlagValid, TokenFlag.FlagNotValid
    };

    /// <summary>
    /// Set of token enclosed flags
    /// </summary>
    private static readonly List<TokenFlag> FlagMaskEnclosed = new List<TokenFlag>
    {
      TokenFlag.FlagEnclosed, TokenFlag.FlagNotEnclosed
    };

    public TokenCategory Category { get; set; }
    public string Content { get; set; }
    public bool Enclosed { get; }

    /// <summary>
    /// Constructs a new token
    /// </summary>
    /// <param name="category">the token category</param>
    /// <param name="content">the token content</param>
    /// <param name="enclosed">whether or not the token is enclosed in braces</param>
    public Token(TokenCategory category, string content, bool enclosed)
    {
      Category = category;
      Content = content;
      Enclosed = enclosed;
    }

    /// <summary>
    /// Validates a token against the <code>flags</code>. The <code>flags</code> is used as a search parameter.
    /// </summary>
    /// <param name="token">the token</param>
    /// <param name="flags">the flags the token must conform against</param>
    /// <returns>true if the token conforms to the set of <code>flags</code>; false otherwise</returns>
    public static bool CheckTokenFlags(Token token, List<TokenFlag> flags)
    {
      // Simple alias to check if flag is a part of the set
      Func<TokenFlag, bool> checkFlag = flags.Contains;

      // Make sure token is the correct closure
      if (flags.Any(f => FlagMaskEnclosed.Contains(f)))
      {
        var success = checkFlag(TokenFlag.FlagEnclosed) == token.Enclosed;
        if (!success) return false; // Not enclosed correctly (e.g. enclosed when we're looking for non-enclosed).
      }

      // Make sure token is the correct category
      if (!flags.Any(f => FlagMaskCategories.Contains(f))) return true;
      var secondarySuccess = false;

      void CheckCategory(TokenFlag fe, TokenFlag fn, TokenCategory c)
      {
        if (secondarySuccess) return;
        var result = checkFlag(fe) ? token.Category == c : checkFlag(fn) && token.Category != c;
        secondarySuccess = result;
      }

      CheckCategory(TokenFlag.FlagBracket, TokenFlag.FlagNotBracket, TokenCategory.Bracket);
      CheckCategory(TokenFlag.FlagDelimiter, TokenFlag.FlagNotDelimiter, TokenCategory.Delimiter);
      CheckCategory(TokenFlag.FlagIdentifier, TokenFlag.FlagNotIdentifier, TokenCategory.Identifier);
      CheckCategory(TokenFlag.FlagUnknown, TokenFlag.FlagNotUnknown, TokenCategory.Unknown);
      CheckCategory(TokenFlag.FlagNotValid, TokenFlag.FlagValid, TokenCategory.Invalid);
      return secondarySuccess;
    }

    /// <summary>
    /// Given a list of <code>tokens</code>, searches for any token token that matches the list of <code>flags</code>.
    /// </summary>
    /// <param name="tokens">the list of tokens</param>
    /// <param name="begin">the search starting position. Inclusive of <code>begin.pos</code></param>
    /// <param name="flags">the search flags</param>
    /// <returns>the search result</returns>
    public static Result FindToken(List<Token> tokens, Result begin, params TokenFlag[] flags)
    {
      return begin?.Pos == null ? Result.GetEmptyResult() : FindTokenBase(tokens, begin.Pos.Value, i => i < tokens.Count, i => i + 1, flags);
    }

    /// <summary>
    /// GIven a list of <code>tokens</code>, searches for any token that matches the list of <code>flags</code>.
    /// </summary>
    /// <param name="tokens">the list of tokens</param>
    /// <param name="flags">the search flags</param>
    /// <returns>the search result</returns>
    public static Result FindToken(List<Token> tokens, params TokenFlag[] flags)
    {
      return FindTokenBase(tokens, 0, i => i < tokens.Count, i => i + 1, flags);
    }

    /// <summary>
    /// Given a list of <code>tokens</code>, searches for the next token in <code>tokens</code> that matches the list of <code>flags</code>.
    /// </summary>
    /// <param name="tokens">the list of tokens</param>
    /// <param name="position">the search starting position. Exclusive</param>
    /// <param name="flags">the search flags</param>
    /// <returns>the search result</returns>
    public static Result FindNextToken(List<Token> tokens, int position, params TokenFlag[] flags)
    {
      return FindTokenBase(tokens, ++position, i => i < tokens.Count, i => i + 1, flags);
    }

    /// <summary>
    /// Given a list of <code>tokens</code>, searches for the next token in <code>tokens</code> that matches the list of <code>flags</code>.
    /// </summary>
    /// <param name="tokens">the list of tokens</param>
    /// <param name="position">the search starting position. Exlusive of position.Pos</param>
    /// <param name="flags">the search flags</param>
    /// <returns>the search result</returns>
    public static Result FindNextToken(List<Token> tokens, Result position, params TokenFlag[] flags)
    {
      return FindTokenBase(tokens, position.Pos.Value + 1, i => i < tokens.Count, i => i + 1, flags);
    }

    /// <summary>
    /// Given a list of <code>tokens</code>, searches for the previous token in <code>tokens</code> that matches the list of <code>flags</code>.
    /// </summary>
    /// <param name="tokens">the list of tokens</param>
    /// <param name="position">the search starting position. Exclusive</param>
    /// <param name="flags">the search flags</param>
    /// <returns>the search result</returns>
    public static Result FindPrevToken(List<Token> tokens, int position, params TokenFlag[] flags)
    {
      return FindTokenBase(tokens, --position, i => i >= 0, i => i - 1, flags);
    }

    /// <summary>
    /// Given a list of <code>tokens</code>, searches for the previous token in <code>tokens</code> that matches the list of <code>flags</code>.
    /// </summary>
    /// <param name="tokens">the list of tokens</param>
    /// <param name="position">the search starting position. Exclusive of position.Pos</param>
    /// <param name="flags">the search flags</param>
    /// <returns>the search result</returns>
    public static Result FindPrevToken(List<Token> tokens, Result position, params TokenFlag[] flags)
    {
      return FindTokenBase(tokens, position.Pos.Value - 1, i => i >= 0, i => i - 1, flags);
    }

    public override bool Equals(object o)
    {
      if (this == o) return true;
      if (!(o is Token)) return false;
      var token = (Token) o;
      return Enclosed == token.Enclosed && Category == token.Category && Equals(Content, token.Content);
    }

    public override int GetHashCode()
    {
      var hashCode = -1776802967;
      hashCode = hashCode * -1521134295 + Category.GetHashCode();
      hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Content);
      hashCode = hashCode * -1521134295 + Enclosed.GetHashCode();
      return hashCode;
    }

    public override string ToString()
    {
      return $"Token{{category={Category}, content='{Content}', enclosed={Enclosed}}}";
    }

    // PRIVATE API

    /// <summary>
    /// Given a list of tokens finds the first token that passes <see cref="CheckTokenFlags"/>.
    /// </summary>
    /// <param name="tokens">the list of the tokens to search</param>
    /// <param name="startIdx">the start index of the search. Inclusive</param>
    /// <param name="shouldContinue">a function that returns whether or not we should continue searching</param>
    /// <param name="next">a function that returns the next search index</param>
    /// <param name="flags">the flags that each token should be validated against</param>
    /// <returns>the found token</returns>
    public static Result FindTokenBase(
      List<Token> tokens,
      int startIdx,
      Func<int, bool> shouldContinue,
      Func<int, int> next,
      params TokenFlag[] flags)
    {
      var find = new List<TokenFlag>();
      find.AddRange(flags);

      for (var i = startIdx; shouldContinue(i); i = next(i))
      {
        var token = tokens[i];
        if (CheckTokenFlags(token, find))
        {
          return new Result(token, i);
        }
      }

      return new Result(null, null);
    }
  }

  /// <summary>
  /// Search result for finds.
  /// </summary>
  public class Result
  {
    public Token Token { get; }
    public int? Pos { get; }

    /// <summary>
    /// Constructs a new search result.
    /// </summary>
    /// <param name="token">the found token</param>
    /// <param name="searchIndex">the index the token was found</param>
    public Result(Token token, int? searchIndex)
    {
      Token = token;
      Pos = searchIndex;
    }

    /** Returns an empty search result. */
    public static Result GetEmptyResult()
    {
      return new Result(null, null);
    }

    public override string ToString()
    {
      return $"Result{{token={Token}, pos={Pos}}}";
    }
  }
}
