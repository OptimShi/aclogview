﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace aclogview.Properties {
    
    
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "15.6.0.0")]
    internal sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase {
        
        private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));
        
        public static Settings Default {
            get {
                return defaultInstance;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string FindOpcodeInFilesRoot {
            get {
                return ((string)(this["FindOpcodeInFilesRoot"]));
            }
            set {
                this["FindOpcodeInFilesRoot"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("0")]
        public int FindOpcodeInFilesOpcode {
            get {
                return ((int)(this["FindOpcodeInFilesOpcode"]));
            }
            set {
                this["FindOpcodeInFilesOpcode"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string FragDatFileToProcess {
            get {
                return ((string)(this["FragDatFileToProcess"]));
            }
            set {
                this["FragDatFileToProcess"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string FragDatFileOutputFolder {
            get {
                return ((string)(this["FragDatFileOutputFolder"]));
            }
            set {
                this["FragDatFileOutputFolder"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string FindTextInFilesRoot {
            get {
                return ((string)(this["FindTextInFilesRoot"]));
            }
            set {
                this["FindTextInFilesRoot"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("5")]
        public byte ProtocolUpdateIntervalDays {
            get {
                return ((byte)(this["ProtocolUpdateIntervalDays"]));
            }
            set {
                this["ProtocolUpdateIntervalDays"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public global::System.DateTime ProtocolLastUpdateCheck {
            get {
                return ((global::System.DateTime)(this["ProtocolLastUpdateCheck"]));
            }
            set {
                this["ProtocolLastUpdateCheck"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool ProtocolCheckForUpdates {
            get {
                return ((bool)(this["ProtocolCheckForUpdates"]));
            }
            set {
                this["ProtocolCheckForUpdates"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool ParsedDataTreeviewDisplayTooltips {
            get {
                return ((bool)(this["ParsedDataTreeviewDisplayTooltips"]));
            }
            set {
                this["ParsedDataTreeviewDisplayTooltips"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("0")]
        public byte PacketsListviewTimeFormat {
            get {
                return ((byte)(this["PacketsListviewTimeFormat"]));
            }
            set {
                this["PacketsListviewTimeFormat"] = value;
            }
        }
    }
}
