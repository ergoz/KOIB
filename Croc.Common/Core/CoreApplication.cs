using System; 

using System.Collections.Generic; 

using System.Configuration; 

using System.Diagnostics; 

using System.Linq; 

using System.Reflection; 

using System.Threading; 

using Croc.Core.Configuration; 

using Croc.Core.Diagnostics; 

using Croc.Core.Diagnostics.Default; 

using Croc.Core.Extensions; 

using Croc.Core.Utils; 

 

 

namespace Croc.Core 

{ 

    /// <summary> 

    /// ?????????? 

    /// </summary> 

    public class CoreApplication : ICoreApplication 

    { 

		// ????? ?????????? 

		Logger _logger = null; 

 

 

		/// <summary> 

        /// ??????? ?????????? 

        /// </summary> 

        public static ICoreApplication Instance 

        { 

            get; 

            private set; 

        } 

        /// <summary> 

        /// ???????????? ?????????? 

        /// </summary> 

        public string Name 

        { 

            get; 

            private set; 

        } 

 

 

        #region ????????????? 

 

 

        /// <summary> 

        /// ??????????? 

        /// </summary> 

        public CoreApplication() 


		{ 

            Instance = this; 

 

 

            // ???????? ?????? ?????????? 

            try 

            { 

                var exeConfig = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None); 

                Config = (ApplicationConfig)exeConfig.GetSection(ApplicationConfig.SectionName); 

                if (Config == null) 

                    throw new Exception("?????? ?? ???????: " + ApplicationConfig.SectionName); 

            } 

            catch (Exception ex) 

            { 

                throw new ConfigurationErrorsException("?????? ????????? ???????????? ??????????", ex); 

            } 

 

 

            // ??????? ??? ?????????? 

            Name = string.IsNullOrEmpty(Config.Name) ? Guid.NewGuid().ToString() : Config.Name; 

			// ?????????????? ?????? 

            InitLogger(); 

			// ???????? ?????????? 

            CreateSubsystems(); 

        } 

 

 

        /// <summary> 

        /// ????????????? ??????? 

        /// </summary> 

        private void InitLogger() 

        { 

            // ??????? ????????? ??????? 

            LogFileFolder = string.IsNullOrEmpty(Config.LogFileFolder) 

                ? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) 

                : Config.LogFileFolder; 

            TraceLevel = GetTraceLevelByName(Config.TraceLevelName, TraceLevel.Error); 

 

 

            EventDispatcher.Init(Config.DiagnosticsConfig); 

            FileWriter.Init(LogFileFolder); 

 

 

            // ???????? ?????? ? ??????? ?????? ?? ????????? ?????? 

            _logger = (Logger)CreateLogger(Name, TraceLevel); 

        } 

 

 

        /// <summary> 

        /// ??????? ?????????? 


        /// </summary> 

        private void CreateSubsystems() 

        { 

            if (Config.Subsystems.Count == 0) 

                throw new ConfigurationErrorsException("?????? ????????? ????"); 

 

 

            foreach (SubsystemConfig subsystemConfig in Config.Subsystems) 

            { 

                Subsystem subsystem; 

                var subsystemName = subsystemConfig.SubsystemName; 

 

 

                try 

                { 

                    // ???????? ?????????? 

                    var type = Type.GetType(subsystemConfig.SubsystemTypeName, true); 

                    subsystem = (Subsystem)Activator.CreateInstance(type); 

                    subsystem.Name = subsystemName; 

 

 

                    // ??????? ? ????????? ????????? 

                    AddSubsystem(subsystemName, subsystem); 

 

 

                    // ??????? ????????? ??????????? 

                    subsystem.TraceLevel = GetTraceLevelByName(subsystemConfig.TraceLevelName, this.TraceLevel); 

                    subsystem.LogFileFolder = string.IsNullOrEmpty(subsystemConfig.LogFileFolder) 

                        ? LogFileFolder 

                        // ??????? ?????????? ???????? ???????????? ?????? ???????? 

                        : LogFileFolder + "/" + subsystemConfig.LogFileFolder; 

                    subsystem.SeparateLog = subsystemConfig.SeparateLog; 

 

 

					subsystem.ConfigUpdated += new EventHandler<ConfigUpdatedEventArgs>(SubsystemConfigUpdated); 

 

 

                    Logger.LogVerbose("??????? ?????????? {0}", subsystemName); 

                } 

                catch (Exception ex) 

                { 

                    Logger.LogException("?????? ???????? ?????????? {0}: {1}", ex, subsystemName, ex.Message); 

                    throw new Exception("?????? ???????? ?????????? " + subsystemName, ex); 

                } 

            } 

 

 

            // ????????????????? ?????????? 

            foreach (SubsystemConfig subsystemConfig in Config.Subsystems) 

            { 


                var subsystemName = subsystemConfig.SubsystemName; 

                try 

                { 

                    var subsystem = GetSubsystem(subsystemName); 

                    subsystem.Init(subsystemConfig); 

                    Logger.LogVerbose("????????? ????????????? ?????????? {0}", subsystemName); 

                } 

                catch (Exception ex) 

                { 

                    Logger.LogException("?????? ????????????? ?????????? {0}: {1}", ex, subsystemName, ex.Message); 

                    throw new Exception("?????? ????????????? ?????????? " + subsystemName, ex); 

                } 

            } 

        } 

 

 

        /// <summary> 

        /// ?????????? ??????? ??????????? ?? ???????? 

        /// </summary> 

        /// <param name="traceLevelName">???????? ?????? ???????????</param> 

        /// <param name="defaultTraceLevel">??????? ??????????? ?? ?????????</param> 

        /// <returns></returns> 

        private static TraceLevel GetTraceLevelByName(string traceLevelName, TraceLevel defaultTraceLevel) 

        { 

            if (string.IsNullOrEmpty(traceLevelName)) 

                return defaultTraceLevel; 

 

 

            try 

            { 

                return (TraceLevel)Enum.Parse(typeof(TraceLevel), traceLevelName); 

            } 

            catch 

            { 

                throw new Exception(string.Format("??????????? ????? ??????? ???????????: '{0}'", traceLevelName)); 

            } 

        } 

 

 

        #endregion 

 

 

        #region ???????????? 

 

 

		/// <summary> 

		/// ?????????? ??????? ????????? ???????????? ?????????? 

		/// </summary> 

		/// <param name="sender"></param> 

		/// <param name="e"></param> 


		private void SubsystemConfigUpdated(object sender, ConfigUpdatedEventArgs e) 

		{ 

			// ??????? ?????????? 

			var subsystem = (ISubsystem)sender; 

 

 

			// ?????????? ????? ???????????? 

			subsystem.ApplyNewConfig(Config.Subsystems[subsystem.Name]); 

 

 

			// ??????? ??????? ???????? ??????? ??? ????? ?????????? 

			ConfigUpdated.RaiseEvent(this, e); 

		} 

 

 

        /// <summary> 

        /// ??????? ???????????? ?????????? 

        /// </summary> 

        public ApplicationConfig Config 

        { 

            get; 

            private set; 

        } 

 

 

        /// <summary> 

        /// ????????? ????? ???????????? ?????????? 

        /// </summary> 

        /// <param name="newConfig">????? ?????? ??????????</param> 

        /// <param name="force">????? ?? ????????? ????? ??????, ???? ???? ?? ?? ?????????? ?? ???????</param> 

        /// <returns> 

        /// true - ???????????? ?????????,  

        /// false - ???????????? ?? ?????????, ?.?. ????? ?? ?????????? ?? ??????? 

        /// </returns> 

        public bool ApplyNewConfig(ApplicationConfig newConfig, bool force) 

        { 

            CodeContract.Requires(newConfig != null); 

 

 

            // ???? ?? ????? ????????? ?????? ? ????? ?????? ?  

            // ????? ?????? ?? ??k??????? ?? ???????? 

            if (!force && Config.Equals(newConfig)) 

                // ?? ????????? ??? ?? ????? 

                return false; 

 

 

            Logger.LogVerbose("?????????? ????? ????????????..."); 

 

 

            // ??????? ?????? ?? ????? 


            Config = newConfig; 

 

 

            // ???????? ????? ??????? ????????? 

            foreach (SubsystemConfig subsystemConfig in Config.Subsystems) 

            { 

                var subsystemName = subsystemConfig.SubsystemName; 

                try 

                { 

                    var subsystem = GetSubsystem(subsystemName); 

                    subsystem.ApplyNewConfig(subsystemConfig); 

                    Logger.LogVerbose("????????? ????????????????? ?????????? {0}", subsystemName); 

                } 

                catch (Exception ex) 

                { 

                    Logger.LogException("?????? ????????????????? ?????????? {0}: {1}", ex, subsystemName, ex.Message); 

                    throw new Exception("?????? ????????????????? ?????????? " + subsystemName, ex); 

                } 

            } 

 

 

            return true; 

        } 

 

 

		/// <summary> 

		/// ??????? ????????? ???????????? ?????????? 

		/// </summary> 

		public event EventHandler<ConfigUpdatedEventArgs> ConfigUpdated; 

 

 

        #endregion 

 

 

        #region ??????????? 

 

 

        /// <summary> 

        /// ??????? ??????????? ?????????? 

        /// </summary> 

        public TraceLevel TraceLevel 

        { 

            get; 

            private set; 

        } 

 

 

        /// <summary> 

        /// ?????, ? ??????? ????? ??????????? ???-????? ?????????? 

        /// </summary> 


        public string LogFileFolder 

        { 

            get; 

            private set; 

        } 

 

 

        /// <summary> 

        /// ?????? ?????????? 

        /// </summary> 

        public ILogger Logger 

        { 

			get { return _logger; } 

        } 

 

 

        /// <summary> 

        /// ??????? ????? ?????? ? ???????? ?????? ? ??????? ???????????, 

        /// ??????? ????? ?????? ? ????????? ????? ? ???????? ????? 

        /// </summary> 

        /// <param name="loggerName">??? ???????</param> 

        /// <param name="traceLevel">??????? ???????????</param> 

        /// <returns></returns> 

        public ILogger CreateLogger(string loggerName, TraceLevel traceLevel) 

        { 

            Logger logger = new Logger(loggerName, new TraceLevelFilter(traceLevel), _logger, _loggerEnabled); 

 

 

            foreach (IEventFilter filter in EventDispatcher.EventFilters) 

            { 

                logger.AddFilter(filter); 

            } 

 

 

            return logger; 

        } 

 

 

        /// <summary> 

        /// ??????? ????????????/????????????? ?????? ? ??????? 

        /// </summary> 

        private ManualResetEvent _loggerEnabled = new ManualResetEvent(true); 

 

 

        /// <summary> 

        /// ??????? ????????????/????????????? ?????? ? ??????? 

        /// </summary> 

        public ManualResetEvent LoggerEnabled 

        { 

            get 


            { 

                return _loggerEnabled; 

            } 

        } 

 

 

        #endregion 

 

 

        #region ?????????? 

 

 

        /// <summary> 

        /// ?????? ????????? 

        /// </summary> 

        private readonly List<KeyValuePair<String, ISubsystem>> _subsystems = 

            new List<KeyValuePair<string, ISubsystem>>(); 

 

 

        /// <summary> 

        /// ????? ??????????, ??????? ????????? ???????? ????????? 

        /// </summary> 

        /// <typeparam name="T">????????????? ?????????</typeparam> 

        /// <returns> 

        /// null - ??????????, ??????????? ???????? ?????????, ?? ??????? 

        /// ?????? ?? ?????????? - ?????? ????????? ??????????, ??????????? ???????? ????????? 

        /// </returns> 

        public T FindSubsystemImplementsInterface<T>() 

        { 

            return (T)_subsystems.FirstOrDefault(i => i.Value is T).Value; 

        } 

 

 

        /// <summary> 

        /// ????? ??????????, ??????? ????????? ???????? ????????? 

        /// </summary> 

        /// <typeparam name="T">????????????? ?????????</typeparam> 

        /// <returns> 

        /// ?????? ?? ?????????? - ?????? ????????? ??????????, ??????????? ???????? ????????? 

        /// </returns> 

        /// <exception cref="System.Exception">??????????, ??????????? ???????? ?????????, ?? ???????</exception> 

        public T FindSubsystemImplementsInterfaceOrThrow<T>() 

        { 

            var res = FindSubsystemImplementsInterface<T>(); 

            if (res == null) 

                throw new ArgumentException("?????????? ?? ???????? ??????????, ??????????? ????????? " + typeof(T).FullName); 

 

 

            return res; 

        } 


 
 

        /// <summary> 

        /// ?????????? ?????????? ?? ?? ??????????, ? ?????? ?? ?????????? ?????????? ?????????? 

        /// </summary> 

        /// <typeparam name="T">????????? ????????????? ??????????</typeparam> 

        /// <returns></returns> 

        public T GetSubsystemOrThrow<T>() where T : ISubsystem 

        { 

            return GetSubsystemOrThrow<T>("?????????? ?? ???????? ?????????? " + typeof(T).FullName); 

        } 

 

 

        /// <summary> 

        /// ?????????? ?????????? ?? ?? ??????????, ? ?????? ?? ?????????? ?????????? ?????????? ? ???????? ???????. 

        /// </summary> 

        /// <typeparam name="T">????????? ????????????? ??????????</typeparam> 

        /// <param name="errorMsg"></param> 

        /// <returns></returns> 

        public T GetSubsystemOrThrow<T>(string errorMsg) where T : ISubsystem 

        { 

            var subsystem = GetSubsystem<T>(); 

            if (subsystem == null) 

                throw new ArgumentException(errorMsg); 

 

 

            return subsystem; 

        } 

 

 

        public IEnumerable<ISubsystem> Subsystems 

        { 

            get { return _subsystems.Select(i => i.Value); } 

        } 

 

 

        /// <summary> 

        /// ????????? ?????????? ? ?????????? 

        /// </summary> 

        /// <remarks> 

        /// ???? ??????????? ??????????? ?????????? (???????? <paramref name="name"/> ????? ?? null), 

        /// ?? ???????? ?? ????? ????? ?????? ?? ???????????? 

        /// </remarks> 

        /// <param name="name">???????????? ?????????? ??? null</param> 

        /// <param name="subsystem">????????? ??????????</param> 

        public void AddSubsystem(String name, ISubsystem subsystem) 

        { 

            CodeContract.Requires(subsystem != null); 

 

 


            foreach (var item in _subsystems) 

                if (!String.IsNullOrEmpty(name) && item.Key == name) 

                    throw new ArgumentException( 

                        "?????????? ??? ???????? ?????????? ? ????????????? " + name); 

 

 

            _subsystems.Add(new KeyValuePair<String, ISubsystem>(name, subsystem)); 

 

 

            // ????????? ???????? ???????? ? ?????????? - ?????? ?? ?????????? 

            subsystem.Application = this; 

        } 

 

 

        public ISubsystem GetSubsystem(String name) 

        { 

            CodeContract.Requires(!string.IsNullOrEmpty(name)); 

 

 

            foreach (var item in _subsystems) 

                if (item.Key == name) 

                    return item.Value; 

 

 

            return null; 

        } 

 

 

        public T GetSubsystem<T>(String name) where T : ISubsystem 

        { 

            CodeContract.Requires(!string.IsNullOrEmpty(name)); 

 

 

            foreach (var item in _subsystems) 

                if (item.Key == name) 

                { 

                    if (!(item.Value is T)) 

                        throw new ArgumentException( 

                            "??????????? ?????????? '" + name + "' ?? ????????? ????????? ????????? " +  

                            typeof(T).FullName); 

 

 

                    return (T)item.Value; 

                } 

 

 

            return default(T); 

        } 

 

 


        /// <summary> 

        /// ?????????? ?????? ?????????? ????????????? runtime-????????? ??????????, 

        /// ???? ?? ????? ???? ???????? ? ??????????? generic ???? (<typeparamref name="T"/>). 

        /// </summary> 

        /// <typeparam name="T">??? ??????????</typeparam> 

        /// <returns>????????? ??? null</returns> 

        public T GetSubsystem<T>() where T : ISubsystem 

        { 

            var foundSubsystems = GetSubsystems<T>(); 

 

 

            if (foundSubsystems.Count > 1) 

                throw new InvalidOperationException( 

                    string.Format("??????? ????? ????? ?????????? ???? {0}", typeof(T).Name)); 

 

 

            if (foundSubsystems.Count == 0) 

                return default(T); 

 

 

            return foundSubsystems[0].Value; 

        } 

 

 

        /// <summary> 

        /// ?????????? ??? ?????????? ?????????? ??? ??????????? ??? <typeparamref name="T"/>. 

        /// </summary> 

        /// <typeparam name="T">??? ??????????.</typeparam> 

        /// <returns>????????? ????????? ????????? ?????? ? ?? ??????????????,  

        /// ??? ???????? ??? ???? ????????? ? ??????????.</returns> 

        public List<KeyValuePair<String, T>> GetSubsystems<T>() where T : ISubsystem 

        { 

            var subsystems_req = new List<KeyValuePair<string, T>>(); 

 

 

            foreach (var item in _subsystems) 

            { 

                if (item.Value is T) 

                    subsystems_req.Add(new KeyValuePair<String, T>(item.Key, (T)item.Value)); 

            } 

 

 

            return subsystems_req; 

        } 

 

 

        #endregion 

 

 

		#region ?????? ?????????? 


 
 

		/// <summary> 

		/// ?????? ?????????? 

		/// </summary> 

        public Version ApplicationVersion 

        { 

            get 

            { 

				foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) 

				{ 

					// ???? ??? ????? ????? ????????? ?????? 

					if (assembly.EntryPoint == null) 

						continue; 

					// ???? ???? ??????? ????? ? ?????? Main ? ??? ?? ?? vshost, ?? ??? ?????? ?????? 

					else if (assembly.EntryPoint.Name == "Main" && assembly.GetName().Name != "vshost") 

						return assembly.GetName().Version; 

				} 

 

 

				// ???? ?????? ?? ????? ?????? ?????? ??????????? ?????? 

				return Assembly.GetExecutingAssembly().GetName().Version; 

			} 

        } 

 

 

		#endregion 

 

 

		#region ?????????? ?????? 

 

 

		/// <summary> 

        /// ??????? ?????????? ?????? ?????????? 

        /// </summary> 

        protected ManualResetEvent _exitEvent = new ManualResetEvent(false); 

 

 

        /// <summary> 

        /// ??????? ?????????? ?????? ?????????? 

        /// </summary> 

        public WaitHandle ExitEvent 

        { 

            get 

            { 

                return _exitEvent; 

            } 

        } 

 

 


		/// <summary> 

		/// ??????? ?????? ?? ?????????? 

		/// </summary> 

		public event EventHandler Exited; 

 

 

        /// <summary> 

        /// ???????, ????? ?????????? ???????? ?????? 

        /// </summary> 

        public void WaitForExit() 

        { 

            _exitEvent.WaitOne(); 

        } 

 

 

        /// <summary> 

        /// ????????? ?????? ?????????? 

        /// </summary> 

        /// <param name="exitType">??? ????????? ?????? ??????????</param> 

        public void Exit(ApplicationExitType exitType) 

        { 

            // ????????? ? ????????? ?????? ??????, ??? ???????????? ?????????? ?????????? 

            // ????? ?????-?????? ??????????, ? ???? ?? ????? ????????? ?????????? ? ?????? ??????, 

            // ?? ??? ?????? ?????? ?? ?????????? ???? ??????????, ?? ?????? ????? ????? ?????????? 

            // ? ?? ?? ?????? ????????? ?????????? ????????? 

            var thread = new Thread(ExitThread); 

            thread.Start(exitType); 

 

 

            // ???? ?????? 

            WaitForExit(); 

        } 

 

 

        /// <summary> 

        /// ????? ??????, ??????? ????????? ?????????? 

        /// </summary> 

        /// <param name="state"></param> 

        private void ExitThread(object state) 

        { 

            // ????????? ?????????? 

            foreach (var pair in _subsystems) 

                Disposer.DisposeObject(pair.Value); 

 

 

            // ????????, ????? ?????????? ?????? ???????? ? ??? ???? ????????? ????? 

            Thread.Sleep(1000); 

            // ??????? ??????? ????????? 

            foreach (var pair in _subsystems) 

                pair.Value.DisposeLogger(); 


 
 

            Logger.LogInfo("?????????? ?????? ?????????? (??? ?????? {0})", 

                state == null ? "?? ?????" : ((ApplicationExitType)state).ToString()); 

 

 

            // ??????? ?????? ?????????? 

            Thread.Sleep(1000); 

            Disposer.DisposeObject(Logger); 

 

 

			// ??????? ??? ????? ???? 

			FileWriter.Close(); 

 

 

            // ????????, ??? ?????? ????????? 

            _exitEvent.Set(); 

			Exited.RaiseEvent(this); 

 

 

            if (state != null) 

                // ??????? ? ?????? ????? 

                // ??????????????, ??? ? ??????????? ?? ????? ???? ????-??????? (??????), 

                // ??? ?????? ???????? ??????, ??? ???????????? ??????????, ??? ???????????? ?? 

                Environment.Exit((int)state); 

 

 

 

 

            // state ????? = null, ????? ????? ?????????? ?? unit-????? 

            // ? ???? ?????? ????????? ?????????? ?????????? ?? ????? 

        } 

 

 

        #endregion 

    } 

}

