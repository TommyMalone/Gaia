using System;

namespace Gaia.Core.Strings
{
    /// <summary>A string reduced to a hash, for names that are compared far more often than they are read.</summary>
    /// <remarks>
    ///     <para>
    ///         The text is hashed once in the constructor, so a comparison is a single integer test and a lookup never walks
    ///         the characters. The text is lower cased before hashing, which makes every comparison case insensitive. The
    ///         original text is kept by <see cref="AiStringTable" /> for debug output.
    ///     </para>
    ///     <para>A default AiString, which a field or an out parameter holds before anything assigns it, is Empty.</para>
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.4.1</seealso>
    public readonly struct AiString : IEquatable<AiString>
    {
        /// <summary>The hash XOR the hash of empty text, so the empty string is stored as zero and a default AiString is Empty.</summary>
        private readonly ulong _hashXorEmpty;

        /// <summary>The hash this string was reduced to.</summary>
        public ulong Hash => _hashXorEmpty ^ AiStringTable.EmptyTextHash;

        /// <summary>Creates a string from its text, registering the text with <see cref="AiStringTable" />.</summary>
        public AiString(string text)
        {
            _hashXorEmpty = AiStringTable.Register(text) ^ AiStringTable.EmptyTextHash;
        }

        /// <summary>The string made from empty text, which a default AiString also is.</summary>
        public static readonly AiString Empty = new(string.Empty);

        /// <summary>The text this string was made from, for debug output.</summary>
        public string Text => AiStringTable.GetText(Hash);

        /// <summary>True if this string is Empty, made from empty text or never assigned.</summary>
        public bool IsEmpty => Hash == Empty.Hash;

        public bool Equals(AiString other)
        {
            return _hashXorEmpty == other._hashXorEmpty;
        }

        public override bool Equals(object? obj)
        {
            return obj is AiString other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Hash.GetHashCode();
        }

        public static bool operator ==(AiString left, AiString right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AiString left, AiString right)
        {
            return !left.Equals(right);
        }

        /// <summary>Lets a caller pass a literal wherever an AiString is expected.</summary>
        public static implicit operator AiString(string text)
        {
            return new AiString(text);
        }

        public override string ToString()
        {
            return Text;
        }
    }
}
