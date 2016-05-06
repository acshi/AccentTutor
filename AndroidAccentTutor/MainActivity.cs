using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.App;

namespace AndroidAccentTutor {
    [Activity(Label = "AndroidAccentTutor", MainLauncher = true, Icon = "@drawable/icon")]
    public class MainActivity : Activity {
        protected override void OnCreate(Bundle bundle) {
            base.OnCreate(bundle);
            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.Main);

            TutorDisplay tutorDisplay = FindViewById<TutorDisplay>(Resource.Id.tutorDisplay);
            tutorDisplay.InitializeAudioAndFft();
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Permission[] grantResults) {
            if (grantResults[0] == Permission.Granted) {
                TutorDisplay tutorDisplay = FindViewById<TutorDisplay>(Resource.Id.tutorDisplay);
                tutorDisplay.TryToGetAudioPermission();
            }
        }
    }
}

