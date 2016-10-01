﻿namespace Evader.UsableAbilities.Items
{
    using Base;

    using Core;

    using Ensage;

    using AbilityType = Core.AbilityType;

    internal class LinkensSphere : Targetable
    {
        #region Constructors and Destructors

        public LinkensSphere(Ability ability, AbilityType type, AbilityFlags flags)
            : base(ability, type, flags)
        {
        }

        #endregion

        #region Public Methods and Operators

        public override bool CanBeCasted(Unit unit)
        {
            if (Variables.Hero.Equals(unit))
            {
                return false;
            }

            return base.CanBeCasted(unit);
        }

        #endregion
    }
}