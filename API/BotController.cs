using Nancy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapAssist.API
{
    public class BotApiModule : NancyModule
    {
        public BotApiModule(IBotStateModel botState)
        {
            Get("/", _ => botState);
        }
    }
}
