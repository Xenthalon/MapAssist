using MapAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapAssist.Automation
{
    interface IRunProfile
    {
        void Abort();

        void Update(GameData gameData, AreaData areaData, List<PointOfInterest> pointsOfInterest);

        void Run();

        bool IsBusy();

        bool HasError();
    }
}
