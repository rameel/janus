﻿namespace Janus.Wpf.Properties {


	// This class allows you to handle specific events on the settings class:
	//  The SettingChanging event is raised before a setting's value is changed.
	//  The PropertyChanged event is raised after a setting's value is changed.
	//  The SettingsLoaded event is raised after the setting values are loaded.
	//  The SettingsSaving event is raised before the setting values are saved.
	public sealed partial class Settings {

		public Settings() {
			PropertyChanged += DoPropertyChanged;
			SettingsSaving += SettingsSavingEventHandler;
			SettingsLoaded += DoSettingsLoaded;
		}

		private void DoSettingsLoaded(object sender, System.Configuration.SettingsLoadedEventArgs e) {
			//throw new System.NotImplementedException();
		}

		private void DoPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
			Save();
		}

		private void SettingsSavingEventHandler(object sender, System.ComponentModel.CancelEventArgs e) {
			// Add code to handle the SettingsSaving event here.
		}
	}
}