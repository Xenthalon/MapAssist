using Nancy;
using Nancy.TinyIoc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapAssist.API
{
    public class CustomNancyBootstrapper : DefaultNancyBootstrapper
    {
        private Automaton _automaton;

        public CustomNancyBootstrapper(Automaton automaton)
        {
            _automaton = automaton;
        }

        protected override void ConfigureApplicationContainer(TinyIoCContainer container)
        {
            base.ConfigureApplicationContainer(container);

            container.Register<IBotStateModel>((c, n) => _automaton.GetState());
        }
    }
}
