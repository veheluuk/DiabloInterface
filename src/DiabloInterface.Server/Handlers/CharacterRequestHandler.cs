using System;
using System.Collections.Generic;
using Zutatensuppe.D2Reader;
using Zutatensuppe.D2Reader.Models;

namespace Zutatensuppe.DiabloInterface.Server.Handlers
{
    public class CharacterRequestHandler : IRequestHandler
    {
        const string InvalidMessage = "Character request handler must accept one argument set to either 'current' or 'active'";

        readonly D2DataReader dataReader;

        public CharacterRequestHandler(D2DataReader dataReader)
        {
            this.dataReader = dataReader;
        }

        public Response HandleRequest(Request request, IList<string> arguments)
        {
            if (arguments.Count == 0)
                throw new RequestHandlerInvalidException(InvalidMessage);

            object payload;
            switch (arguments[0].ToLowerInvariant())
            {
                case "active":
                    payload = BuildPayload(dataReader.ActiveCharacter);
                    break;
                case "current":
                    payload = BuildPayload(dataReader.CurrentCharacter);
                    break;
                default:
                    throw new RequestHandlerInvalidException(InvalidMessage);
            }

            return new Response()
            {
                Payload = payload,
                Status = payload != null ? ResponseStatus.Success : ResponseStatus.NotFound,
            };
        }

        object BuildPayload(Character character)
        {
            if (character == null)
                return null;

            return new
            {
                IsCurrentCharacter = character == dataReader.CurrentCharacter,
                character.Name,
                character.Created,
                character.CharClass,
                character.IsHardcore,
                character.IsExpansion,
                character.IsDead,
                character.Deaths,
                character.Level,
                character.Experience,
                Stats = new
                {
                    character.Strength,
                    character.Dexterity,
                    character.Vitality,
                    character.Energy
                },
                Resistances = new
                {
                    Fire = character.FireResist,
                    Cold = character.ColdResist,
                    Lightning = character.LightningResist,
                    Poison = character.PoisonResist
                },
                character.Gold,
                character.GoldStash,
                character.FasterCastRate,
                character.FasterHitRecovery,
                character.FasterRunWalk,
                character.IncreasedAttackSpeed,
                character.MagicFind
            };
        }
    }
}
