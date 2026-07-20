using System;
using Gaia.Core.Temp;

namespace Gaia.Core.Considerations
{
    public class EntityExists : ConsiderationBase
    {
        /// <inheritdoc />
        public override bool Init(AiCreationData creationData)
        {
            // Configurations cannot presently be loaded successfully.
            return false;
        }

        /// <inheritdoc />
        public override void Calculate()
        {
            throw new NotImplementedException();
        }
    }
}
