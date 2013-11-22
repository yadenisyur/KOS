﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace kOS
{
    public class CPU : ExecutionContext
    {
        public object Parent;
        public enum Modes { READY, STARVED, OFF };
        public Modes Mode = Modes.READY;
        public String Context;
        public Archive archive;
        public BindingManager bindingManager;
        public float SessionTime;
        public int ClockSpeed = 5;
        
        private readonly Dictionary<String, Variable> variables = new Dictionary<String, Variable>();
        private Volume selectedVolume = null;
        private List<Volume> volumes = new List<Volume>();
        private readonly List<kOSExternalFunction> externalFunctions = new List<kOSExternalFunction>();
        
        public override Vessel Vessel { get { return ((kOSProcessor)Parent).vessel; } }
        public override Dictionary<String, Variable> Variables { get { return variables; } }
        public override List<Volume> Volumes { get  { return volumes; } }
        public override List<kOSExternalFunction> ExternalFunctions { get { return externalFunctions; } }

        public static kOSRunType RunType = kOSRunType.KSP;
        public enum kOSRunType { KSP, WINFORMS };
        
        public override Volume SelectedVolume
        {
            get { return selectedVolume; }
            set { selectedVolume = value; }
        }

        public CPU(object parent, string context)
        {
            this.Parent = parent;
            this.Context = context;
            
            bindingManager = new BindingManager(this, Context);

            if (context == "ksp")
            {
                RunType = kOSRunType.KSP;

                archive = new Archive(Vessel);
                Volumes.Add(archive);
            }
            else
            {
                RunType = kOSRunType.WINFORMS;
            }

            this.RegisterkOSExternalFunction(new object[] { "test2", this, "testFunction", 2 });
        }

        public double testFunction(double x, double y) { return x * y; }

        public void RegisterkOSExternalFunction(object[] parameters)
        {
            if (parameters.Count() != 4) return;

            var name = (String)parameters[0];
            var parent = parameters[1];
            var methodName = (String)parameters[2];
            var parameterCount = (int)parameters[3];

            RegisterkOSExternalFunction(name, parent, methodName, parameterCount);
        }

        public void RegisterkOSExternalFunction(String name, object parent, String methodName, int parameterCount)
        {
            externalFunctions.Add(new kOSExternalFunction(name.ToUpper(), parent, methodName, parameterCount));
        }

        public override object CallExternalFunction(string name, string[] parameters)
        {
            var callFound = false;
            var callAndParamCountFound = false;

            foreach (var function in ExternalFunctions.Where(function => function.Name == name.ToUpper()))
            {
                callFound = true;

                if (function.ParameterCount != parameters.Count()) continue;
                callAndParamCountFound = true;

                var t = function.Parent.GetType();
                var method = t.GetMethod(function.MethodName);

                // Attempt to cast the strings to types that the target method is expecting
                var parameterInfoArray = method.GetParameters();
                var convertedParams = new object[parameters.Length];
                for (var i = 0; i < parameters.Length; i++)
                {
                    var paramType = parameterInfoArray[i].ParameterType;
                    var value = parameters[i];
                    object converted = null;
                            
                    if (paramType == typeof(String))
                    {
                        converted = parameters[i];
                    }
                    else if (paramType == typeof(float))
                    {
                        float flt;
                        if (float.TryParse(value, out flt)) converted = flt;
                    }
                    else if (paramType == typeof(double))
                    {
                        double dbl;
                        if (double.TryParse(value, out dbl)) converted = dbl;
                    }
                    else if (paramType == typeof(int))
                    {
                        int itgr;
                        if (int.TryParse(value, out itgr)) converted = itgr;
                    }
                    else if (paramType == typeof(long))
                    {
                        long lng;
                        if (long.TryParse(value, out lng)) converted = lng;
                    }
                    else if (paramType == typeof(bool))
                    {
                        bool bln;
                        if (bool.TryParse(value, out bln)) converted = bln;
                    }

                    if (converted == null) throw new kOSException("Parameter types don't match");
                    convertedParams[i] = converted;
                }

                return method.Invoke(function.Parent, convertedParams);
            }

            if (!callFound) throw new kOSException("External function '" + name + "' not found");
            if (!callAndParamCountFound) throw new kOSException("Wrong number of arguments for '" + name + "'");

            return null;
        }

        public override bool FindExternalFunction(string name)
        {
            return ExternalFunctions.Any(function => function.Name == name.ToUpper());
        }

        public void Boot()
        {
            Mode = Modes.READY;

            Push(new InterpreterBootup(this));

            SelectedVolume = Volumes.Count > 1 ? Volumes[1] : Volumes[0];
        }

        public bool IsAlive()
        {
            var partState = ((kOSProcessor)Parent).part.State;

            if (partState == PartStates.DEAD)
            {
                Mode = Modes.OFF;
                return false;
            }

            return true;
        }

        public void AttachHardDisk(Harddisk hardDisk)
        {
            Volumes.Add(hardDisk);
            SelectedVolume = hardDisk;
        }

        public override void VerifyMount()
        {
            selectedVolume.CheckRange();
        }

        internal void ProcessElectricity(Part part, float time)
        {
            if (Mode == Modes.OFF) return;

            var electricReq = 0.01f * time;
            var result = part.RequestResource("ElectricCharge", electricReq) / electricReq;

            var newMode = (result < 0.5f) ? Modes.STARVED : Modes.READY;

            if (newMode == Modes.READY && Mode == Modes.STARVED)
            {
                Boot();
            }

            Mode = newMode;
        }

        public override bool SwitchToVolume(int volID)
        {
            if (Volumes.Count > volID)
            {
                var newVolume = Volumes[volID];

                if (newVolume.CheckRange())
                {
                    SelectedVolume = newVolume;
                    return true;
                }
                throw new kOSException("Volume disconnected - out of range");
            }

            return false;
        }

        public override bool SwitchToVolume(string targetVolume)
        {
            foreach (var volume in Volumes.Where(volume => volume.Name.ToUpper() == targetVolume.ToUpper()))
            {
                if (volume.CheckRange())
                {
                    SelectedVolume = volume;
                    return true;
                }
                throw new kOSException("Volume disconnected - out of range");
            }

            return false;
        }

        public override BoundVariable CreateBoundVariable(string varName)
        {
            varName = varName.ToLower();

            if (FindVariable(varName) != null)
            {
                throw new kOSException("Cannot bind " + varName + "; name already taken.");
            }
            variables.Add(varName, new BoundVariable());
            return (BoundVariable) variables[varName];
        }

        public override void Update(float time)
        {
            bindingManager.Update(time);

            SessionTime += time;

            for (var i = 0; i < ClockSpeed; i++)
            {
                base.Update(time / ClockSpeed);
            }

            switch (Mode)
            {
                case Modes.STARVED:
                    ChildContext = null;
                    break;
                case Modes.OFF:
                    ChildContext = null;
                    break;
            }

            // After booting
            if (ChildContext == null)
            {
                Push(new ImmediateMode(this));
            }
        }

        public override void SendMessage(SystemMessage message)
        {
            switch (message)
            {
                case SystemMessage.SHUTDOWN:
                    ChildContext = null;
                    Mode = Modes.OFF;
                    break;

                case SystemMessage.RESTART:
                    ChildContext = null;
                    Boot();
                    break;

                default:
                    base.SendMessage(message);
                    break;
            }
        }

        internal void UpdateVolumeMounts(List<Volume> attachedVolumes)
        {
            // Remove volumes that are no longer attached
            foreach (Volume volume in new List<Volume>(volumes))
            {
                if (!(volume is Archive) && !attachedVolumes.Contains(volume))
                {
                    volumes.Remove(volume);
                }
            }

            // Add volumes that have become attached
            foreach (Volume volume in attachedVolumes.Where(volume => !volumes.Contains(volume)))
            {
                volumes.Add(volume);
            }
        }

        public override void OnSave(ConfigNode node)
        {
            var contextNode = new ConfigNode("context");

            // Save variables
            if (Variables.Count > 0)
            {
                var varNode = new ConfigNode("variables");

                foreach (var kvp in Variables.Where(kvp => !(kvp.Value is BoundVariable)))
                {
                    varNode.AddValue(kvp.Key, File.EncodeLine(kvp.Value.Value.ToString()));
                }

                contextNode.AddNode(varNode);
            }

            if (ChildContext != null)
            {
                ChildContext.OnSave(contextNode);
            }

            node.AddNode(contextNode);
        }

        public override void OnLoad(ConfigNode node)
        {
            foreach (var contextNode in node.GetNodes("context"))
            {
                foreach (var varNode in contextNode.GetNodes("variables"))
                {
                    foreach (ConfigNode.Value value in varNode.values)
                    {
                        var newVar = CreateVariable(value.name);
                        newVar.Value = new Expression(File.DecodeLine(value.value), this).GetValue();
                    }
                }
            }
        }

        public override string GetVolumeBestIdentifier(Volume SelectedVolume)
        {
            var localIndex = volumes.IndexOf(SelectedVolume);

            if (!String.IsNullOrEmpty(SelectedVolume.Name)) return "#" + localIndex + ": \"" + SelectedVolume.Name + "\"";
            return "#" + localIndex;
        }
    }
}
