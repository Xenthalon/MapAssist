using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapAssist.API
{
    public interface IBotStateModel
    {
        bool InGame { get; set; }
        uint MapSeed { get; set; }
        string CharacterName { get; set; }
    }
}
