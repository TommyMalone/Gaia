using System;

namespace Gaia.Authoring.Documents
{
    /// <summary>The scope <see cref="AuthoringDocument.Transaction" /> hands out, whose disposal ends the transaction.</summary>
    /// <remarks>
    ///     A second disposal does nothing, so a scope disposed by a using statement and again by hand ends one
    ///     transaction, not two.
    /// </remarks>
    internal sealed class DocumentTransaction : IDisposable
    {
        private readonly AuthoringDocument _document;
        private bool _isDisposed;

        /// <summary>Creates the scope of a transaction the document has opened.</summary>
        public DocumentTransaction(AuthoringDocument document)
        {
            _document = document;
        }

        /// <summary>Ends the transaction on the earliest call, and does nothing on any call after it.</summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _document.EndTransaction();
        }
    }
}
