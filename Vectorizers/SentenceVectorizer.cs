using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using BotBotNLP.NeuralNetwork.Sparse;

namespace BotBotNLP.Vectorizers
{
  public class SentenceVectorizer {
    public int HashingBins {get; private set;}

    public bool UseHashingTrick {get; set;} = true;
    public IWordVectorReader WordVectorReader {get; set;}

    public int SentenceEmbeddingDim {
      get {
        return this.UseHashingTrick
          ? this.WordVectorReader.EmbeddingDim + this.HashingBins
          : this.WordVectorReader.EmbeddingDim;
      }
    }
    
    public SentenceVectorizer(IWordVectorReader wordVectorReader, int hashingBins = 10000000) {
      this.WordVectorReader = wordVectorReader;
      this.HashingBins = hashingBins;
    }

    private Regex word_tokenize = new Regex(@"[a-zA-Z]+|\d+|[^a-zA-Z\d\s]+",
      RegexOptions.Compiled | RegexOptions.CultureInvariant);
    public string[] SentenceToWords(string sentence) {
      return this.word_tokenize
        .Matches(sentence)
        .Select(match => match.Value)
        .ToArray();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private UInt64 CalculateHash(string input) {
      UInt64 hashedValue = 3074457345618258791ul;
      for(int i=0; i < input.Length; i++)
      {
          hashedValue += input[i];
          hashedValue *= 3074457345618258799ul;
      }
      return hashedValue;
    }

    private string[] GetBiGramList(string[] words) {
      var bigrams = new List<string>();
      if (words.Length > 2) {
        for (var i = 1; i < words.Length; i++) {
          bigrams.Add(words[i-1] + words[i]);
        }
      }
      return bigrams.ToArray();
    }

    public SparseVector<double> SentenceToVector(string sentence) {
      var words = this.SentenceToWords(sentence.Trim().ToLowerInvariant());
      if (words.Length == 0) {
        return null;
      }
      else {
        var wordvec_embeds = new double[this.WordVectorReader.EmbeddingDim];
        // Sum all word vectors
        foreach (var word in words) {
          var wordEmbeds = this.WordVectorReader.GetWordVector(word);
          for (var i = 0; i < this.WordVectorReader.EmbeddingDim; i++) {
            wordvec_embeds[i] += wordEmbeds[i];
          }
        }

        if (words.Length > 1) {
          for (var i = 0; i < this.WordVectorReader.EmbeddingDim; i++) {
            wordvec_embeds[i] = wordvec_embeds[i] / words.Length;
          }
        }
        
        if (!this.UseHashingTrick) {
          var result = new SparseVector<double>(this.WordVectorReader.EmbeddingDim);
          SparseVector<double>.Copy(wordvec_embeds, result);
          return result;
        }
        else {
          var wordvec_dim = this.WordVectorReader.EmbeddingDim;
          var embeddingDim =  this.SentenceEmbeddingDim;
          
          var embeds = new SparseVector<double>(embeddingDim);
          SparseVector<double>.Copy(wordvec_embeds, embeds, this.WordVectorReader.EmbeddingDim);

          if (words.Length > 2) {
            var bigrams = this.GetBiGramList(words);
            foreach (var bigram in bigrams) {
              var hash = this.CalculateHash(bigram);
              var hash_loc = hash % ((UInt64)this.HashingBins - 1) + 1;
              embeds[(int)((UInt64)wordvec_dim + hash_loc)] = 1;
            }
          }

          return embeds;
        }
      }
    }
  }
}