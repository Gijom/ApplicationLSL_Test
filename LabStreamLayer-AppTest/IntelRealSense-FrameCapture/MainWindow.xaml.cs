using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Intel.RealSense;
using LSL;
using System.Runtime.InteropServices;

namespace Intel.RealSense
{
    /**
     * This code will send frame markers to LSL so that videos that are recorded using the tool can be parsed effectively in post-processing.
     * We might need a better protoccol so that too much video is not recorded so that it is more efficient.
     * **/

    public partial class CaptureWindow : Window
    {
        // Intel RealSense Variables
        private Pipeline pipeline;
        private Colorizer colorizer;
        private Align align;
        private CancellationTokenSource tokenSource = new CancellationTokenSource();

		private CustomProcessingBlock processBlock;

        // Lab Streaming Layer Variables
        /**
         * Identifying Variables: Process ID; Stream Name; Type of Data; Sampling Rate
         * **/
        private const string guid = "33C3D35A-51FF-44D9-8F05-81E972FA2F62"; // Unique Process ID -- Pre-Generated

        private string lslStreamName = "Intel RealSense Camera";
        private string lslStreamType = "Frame-Markers";
        private double sampling_rate = liblsl.IRREGULAR_RATE; // Default Value

        private liblsl.StreamInfo lslStreamInfo;
        private liblsl.StreamOutlet lslOutlet = null; // The Streaming Outlet
        private int lslChannelCount = 2; // Number of Channels to Stream by Default

        private const liblsl.channel_format_t lslChannelFormat = liblsl.channel_format_t.cf_string; // Stream Variable Format

        private string[] sample; // Data Samples to be Pushed into LSL
        private ulong lastDepthFrame = 0;
        private ulong lastColorFrame = 0;

        private const string defaultDirectory = ".\\Recordings"; // Where the recordings are Stashed. TOFIX change this before release
        private string fileRecording = "";


        private ImageCodecInfo pngCodecInf;
        private System.Drawing.Imaging.Encoder myEncoder;
        private EncoderParameter myEncoderParameter;
        private EncoderParameters myEncoderParameters;
	

        private void UploadImage(System.Windows.Controls.Image img, VideoFrame frame)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                if (frame.Width == 0) return;

                var bytes = new byte[frame.Stride * frame.Height];
                frame.CopyTo(bytes);

                var bs = BitmapSource.Create(frame.Width, frame.Height,
                                  300, 300,
                                  PixelFormats.Rgb24,
                                  null,
                                  bytes,
                                  frame.Stride);

                var imgSrc = bs as ImageSource;

                img.Source = imgSrc;
            }));
        }
        
        public CaptureWindow()
        {
            InitializeComponent();
        }

		public CaptureWindow(string pIDValue)
		{
			InitializeComponent();
			this.idTextBox.Text = pIDValue;

			LinkLabStreamingLayer();
		}

        /**
         * NOTES 
         * Curently it records immediately after linking the program with LabStreamLayer and there is a consumer of the data. 
         * There might be a better solution, but we don't want to increase the number of button presses for the protoccol. It is probably better to record more than to forget pressing 
         * the record button before an experiment. 
         * 
         * **/
        // Code Taken Directly from the LibRealSense 2 Examples -- Captures and Displays Depth and RGB Camera.
        private void startRecordingProcess()
        {
            try
            {
                pipeline = new Pipeline();
                colorizer = new Colorizer();
                align = new Align(Stream.Color);

                var cfg = new Config();
                cfg.EnableStream(Stream.Depth, 640, 480, Format.Z16,30);
                cfg.EnableStream(Stream.Color, 640, 480, Format.Rgb8,30);
                
                var pp = pipeline.Start(cfg);

                //Data recording as rosbag  // Preferred method to not lose frame number information
                RecordDevice recordDevice = new RecordDevice(pp.Device, fileRecording + ".bag");
                recordDevice.Pause();

                //Load custom parameters to improve depth sensor quality for application
                var adv = AdvancedDevice.FromDevice(pp.Device);
                adv.JsonConfiguration = File.ReadAllText("default.json").Replace("\r\n", "\n");

                // Get the recommended processing blocks for the depth sensor
                var sensorDepth = pp.Device.QuerySensors<Sensor>().First(s => s.Is(Extension.DepthSensor));
                var blocks = sensorDepth.ProcessingBlocks.ToList();

                // Set Auto Exposure Priority OFF for RGB Camera
                /*
                var sensorRGB = pp.Device.QuerySensors<Sensor>().First(s => s.Is(Extension.Video));
                foreach (var opt in sensorRGB.Options)
                {
                    if (opt.Key == Option.AutoExposurePriority)
                    {
                        opt.Value = 0f;
                    }
                }
                */


                pngCodecInf = GetEncoderInfo("image/png");
                myEncoder = System.Drawing.Imaging.Encoder.Quality;
                myEncoderParameters = new EncoderParameters(1);
                // quality level of encoder
                myEncoderParameter = new EncoderParameter(myEncoder, 100L);
                myEncoderParameters.Param[0] = myEncoderParameter;

                processBlock = new CustomProcessingBlock((f, src) =>
                {
                    // We create a FrameReleaser object that would track
                    // all newly allocated .NET frames, and ensure deterministic finalization
                    // at the end of scope. 
                    using (var releaser = new FramesReleaser())
                    {
                        //Apply recommended processing blocks (includes hole filter which we don't want)
                        /*
                        foreach (ProcessingBlock p in blocks)
                            f = p.Process(f).DisposeWith(releaser);
                        */

                        //Use the align filter to visually align color and depth frames
                        //f = f.ApplyFilter(align).DisposeWith(releaser);

                        var frames = f.As<FrameSet>().DisposeWith(releaser);

                        var colorFrame = frames[Stream.Color].DisposeWith(releaser);
                        var depthFrame = frames[Stream.Depth].DisposeWith(releaser);

                        // Combine the frames into a single result
                        var res = src.AllocateCompositeFrame(depthFrame, colorFrame).DisposeWith(releaser);
                        // Send it to the next processing stage
                        src.FrameReady(res);
                    }
                });

                processBlock.Start(f =>
				{
                    using (var frames = f.As<FrameSet>())
                    {
                        var color_frame = frames.ColorFrame.DisposeWith(frames);
                        var depth_frame = frames.DepthFrame.DisposeWith(frames);
                        var colorized_depth = colorizer.Process<VideoFrame>(depth_frame).DisposeWith(frames);

                        UploadImage(imgDepth, colorized_depth);
						UploadImage(imgColor, color_frame);
                       

                        if (lslOutlet != null & lslOutlet.have_consumers())
						{

                            
                            // if first sample or new frame
                            if (color_frame.Number == 0 | lastColorFrame != color_frame.Number)
                            {
                                Bitmap bmpColor = new Bitmap(color_frame.Width, color_frame.Height, color_frame.Stride, System.Drawing.Imaging.PixelFormat.Format24bppRgb, color_frame.Data).DisposeWith(frames);

                                //bmpColor.Save(fileRecording + String.Format("color_{0}.png", color_frame.Number.ToString()), pngCodecInf,myEncoderParameters);
                                
                                lastColorFrame = color_frame.Number;
                            }
                            else
                            {
                                Debug.WriteLine("Color frame not updated");
                            }
                            

                            // if first sample or new frame
                            if (depth_frame.Number == 0 | lastDepthFrame!= depth_frame.Number)
                            {
                                //TOFIX having issues with recording the 16 bit grayscale depth data
                                //Bitmap bmpDepth = new Bitmap(depth_frame.Width, depth_frame.Height, depth_frame.Stride, System.Drawing.Imaging.PixelFormat.Format16bppGrayScale, depth_frame.Data).DisposeWith(frames);
                                //bmpDepth.Save(fileRecording + String.Format("depth_{0}.png", depth_frame.Number.ToString()), pngCodecInf, myEncoderParameters);

                                Bitmap bmpColorDepth = new Bitmap(colorized_depth.Width, colorized_depth.Height, colorized_depth.Stride, System.Drawing.Imaging.PixelFormat.Format24bppRgb, colorized_depth.Data).DisposeWith(frames);

                                //bmpColorDepth.Save(fileRecording + String.Format("depthClr_{0}.png", colorized_depth.Number.ToString()), pngCodecInf, myEncoderParameters);

                                lastDepthFrame = depth_frame.Number;
                                
                            }
                            else
                            {
                                Debug.WriteLine("Depth frame not updated");
                            }
                            

                            //rosbag record resume
                            recordDevice.Resume();

							// Do LSL Streaming Here
							sample[0] = "" + depth_frame.Number + "_" + depth_frame.Timestamp;
							sample[1] = "" + color_frame.Number + "_" + color_frame.Timestamp;
							lslOutlet.push_sample(sample, liblsl.local_clock());
						}
                        else
                        {
                            //rosbag record pause
                            recordDevice.Pause();
                        }
					}
				});


                var token = tokenSource.Token;

                var t = Task.Factory.StartNew(() =>
                {
                    // Main Loop -- 
                    while (!token.IsCancellationRequested)
                    {
                        using (var frames = pipeline.WaitForFrames())
						{
							processBlock.Process(frames);
						}
                    }

                }, token);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                Application.Current.Shutdown();
            }
        }

        private void LinkLabStreamingLayer()
        {
            if (lslOutlet == null)
            {
                sample = new string[lslChannelCount];

                lslStreamInfo = new liblsl.StreamInfo(lslStreamName + "-" + idTextBox.Text, lslStreamType, lslChannelCount, sampling_rate, lslChannelFormat, guid + "-" + idTextBox.Text);
                lslOutlet = new liblsl.StreamOutlet(lslStreamInfo);

                infoTextBox.Text += "Linked to Lab Streaming Layer\nNow Streaming Frame Data\n";
            }
            
            // Once linked no need for the button functionality so disable it.
            lslLink.Content = "Camera is Linked";
            lslLink.IsEnabled = false;

            // Disable the Experiment ID Text Functionality
            idTextBox.IsEnabled = false;

            checkDirectory();


            startRecordingProcess();
        }

        private void checkDirectory()
        {
            if (!Directory.Exists(defaultDirectory))
            {
                Directory.CreateDirectory(defaultDirectory);
            }

            int fileInc = 1;
            fileRecording = defaultDirectory + "\\" + idTextBox.Text + "_" + fileInc;

			foreach (string f in Directory.GetFiles(defaultDirectory))
			{
				string coreName = f.Split('-')[0];

				if (coreName.Equals(fileRecording))
				{
					fileInc += 1;
					fileRecording = defaultDirectory + "\\" + idTextBox.Text + "_" + fileInc;
				}

			}
            infoTextBox.Text += "Recording File = " + fileRecording;

        }

        private static ImageCodecInfo GetEncoderInfo(String mimeType)
        {
            int j;
            ImageCodecInfo[] encoders;
            encoders = ImageCodecInfo.GetImageEncoders();
            for (j = 0; j < encoders.Length; ++j)
            {
                if (encoders[j].MimeType == mimeType)
                    return encoders[j];
            }
            return null;
        }


        // Interface Controls Go Here
        private void control_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
			tokenSource.Cancel();
			
			while (pipeline != null)
			{
				pipeline.Stop();
				pipeline = null;
			}
		}

        private void lslLink_Click(object sender, RoutedEventArgs e)
        {
            // Link LabStreamLayer
            LinkLabStreamingLayer();
        }


    }
}
