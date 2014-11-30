//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.SkeletonBasics
{
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using Microsoft.Kinect;
    using System.Windows.Media.Imaging;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// Puntuación
        public  static int puntuacion = 0;

        /// Ángulo en el que ambas piernas deben separarse
        private int angulo_separar = 20;

        /// Series, repeticiones y margen de error del ejercicio
        public static int series = 0, repeticiones;
        public static double m_error;

        /// Variables a mostrar en la ventana principal
        private int series_restantes, repeticiones_restantes;
        int ejercicio_actual; // Brazos en cruz -> 0, Relajado-> 1, piernas abiertas brazos arriba -> 2, piernas cerradas brazos abajo -> 3
        private string[] nombre_ejercicio;
 

        /// <summary>
        /// Width of output drawing
        /// </summary>
        private const float RenderWidth = 640.0f;

        /// <summary>
        /// Height of our output drawing
        /// </summary>
        private const float RenderHeight = 480.0f;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of body center ellipse
        /// </summary>
        private const double BodyCenterThickness = 10;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Brush used to draw skeleton center point
        /// </summary>
        private readonly Brush centerPointBrush = Brushes.Blue;

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush JointBrush = Brushes.Red;





        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Drawing group for skeleton rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;

        /// <summary>
        /// Bitmap that will hold color information
        /// </summary>
        private WriteableBitmap colorBitmap;

        /// <summary>
        /// Intermediate storage for the color data received from the camera
        /// </summary>
        private byte[] colorPixels;


        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            this.nombre_ejercicio = new string[5];
            this.nombre_ejercicio[0] = "Brazos en cruz";
            this.nombre_ejercicio[1] = "Brazos pegados al cuerpo";
            this.nombre_ejercicio[2] = "Brazos por encima de la cabeza. Piernas abiertas";
            this.nombre_ejercicio[3] = "Brazos relajados. Piernas cerradas";
        }



        /// <summary>
        /// Draws indicators to show which edges are clipping skeleton data
        /// </summary>
        /// <param name="skeleton">skeleton to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private static void RenderClippedEdges(Skeleton skeleton, DrawingContext drawingContext)
        {
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, RenderHeight - ClipBoundsThickness, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, RenderHeight));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(RenderWidth - ClipBoundsThickness, 0, ClipBoundsThickness, RenderHeight));
            }
        }

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            // Se llama a la ventana de inicio donde se introducen los datos del ejercicio
            Window1 ventana_inicio = new Window1();
            ventana_inicio.ShowDialog();
            ejercicio_actual = 0;
            // Se actualizan los contadores
            this.series_restantes = series;
            this.repeticiones_restantes = repeticiones;
            this.campo_series_restante.Content = series;
            this.campo_repeticiones_restantes.Content = repeticiones;
            this.campo_ejercicio.Content = this.nombre_ejercicio[this.ejercicio_actual];

            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            // Display the drawing using our image control
            this.SkeletalImage.Source = this.imageSource;

            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit (See components in Toolkit Browser).
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (null != this.sensor)
            {

                // Turn on the color stream to receive color frames
                this.sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);

                // Allocate space to put the pixels we'll receive
                this.colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];

                // This is the bitmap we'll display on-screen
                this.colorBitmap = new WriteableBitmap(this.sensor.ColorStream.FrameWidth, this.sensor.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

                // Set the image we display to point to the bitmap where we'll put the image data
                this.ColorImage.Source = this.colorBitmap;

                // Add an event handler to be called whenever there is new color frame data
                this.sensor.ColorFrameReady += this.SensorColorFrameReady;

                // Turn on the skeleton stream to receive skeleton frames
                this.sensor.SkeletonStream.Enable();

                // Add an event handler to be called whenever there is new color frame data
                this.sensor.SkeletonFrameReady += this.SensorSkeletonFrameReady;

                // Start the sensor!
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }

            if (null == this.sensor)
            {
                this.statusBarText.Text = Properties.Resources.NoKinectReady;
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }
        }

        private void SensorColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (colorFrame != null)
                {
                    // Copy the pixel data from the image to a temporary array
                    colorFrame.CopyPixelDataTo(this.colorPixels);

                    // Write the pixel data into our bitmap
                    this.colorBitmap.WritePixels(
                        new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                        this.colorPixels,
                        this.colorBitmap.PixelWidth * sizeof(int),
                        0);
                }
            }
        }

        /// <summary>
        /// Event handler for Kinect sensor's SkeletonFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons = new Skeleton[0];

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }
            }

            using (DrawingContext dc = this.drawingGroup.Open())
            {
                // Draw a transparent background to set the render size
                dc.DrawRectangle(Brushes.Transparent, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                if (skeletons.Length != 0)
                {
                    foreach (Skeleton skel in skeletons)
                    {
                        RenderClippedEdges(skel, dc);

                        if (skel.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            this.DrawBonesAndJoints(skel, dc);
                        }
                        else if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
                        {
                            dc.DrawEllipse(
                            this.centerPointBrush,
                            null,
                            this.SkeletonPointToScreen(skel.Position),
                            BodyCenterThickness,
                            BodyCenterThickness);
                        }
                    }
                }

                // prevent drawing outside of our render area
                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            }
        }


        private bool BrazoIzquierdoLevantado(Skeleton skeleton)
        {
            float long_brazo = System.Math.Abs(skeleton.Joints[JointType.Spine].Position.Y - skeleton.Joints[JointType.Head].Position.Y);
            double mu_y = skeleton.Joints[JointType.WristLeft].Position.Y;
            double co_y = skeleton.Joints[JointType.ElbowLeft].Position.Y;
            double dif = System.Math.Abs(mu_y - co_y);
            bool correcto = dif < m_error;
  
            return correcto;
        }

        private bool BrazoDerechoLevantado(Skeleton skeleton)
        {
            float long_brazo = System.Math.Abs(skeleton.Joints[JointType.Spine].Position.Y - skeleton.Joints[JointType.Head].Position.Y);
            double mu_y = skeleton.Joints[JointType.WristRight].Position.Y;
            double co_y = skeleton.Joints[JointType.ElbowRight].Position.Y;
            double dif = System.Math.Abs(mu_y - co_y);
            bool correcto = dif < m_error;
 
            return correcto;
        }

        private bool PiernasEnElSuelo(Skeleton skeleton) {
            double dif = System.Math.Abs(skeleton.Joints[JointType.FootLeft].Position.Y - skeleton.Joints[JointType.FootRight].Position.Y);
            bool correcto = dif < m_error;
 
            
            
            return correcto;

        }

        private bool BrazoIzquierdoPegadoCuerpo(Skeleton skeleton) {
            double dif_codo_muneca = System.Math.Abs(skeleton.Joints[JointType.ElbowLeft].Position.X - skeleton.Joints[JointType.WristLeft].Position.X);
            bool correcto = dif_codo_muneca < m_error && skeleton.Joints[JointType.WristLeft].Position.Y < skeleton.Joints[JointType.ShoulderLeft].Position.Y;
 
              return correcto;
        
        }

        private bool BrazoDerechoPegadoCuerpo(Skeleton skeleton)
        {
            double dif_codo_muneca = System.Math.Abs(skeleton.Joints[JointType.ElbowRight].Position.X - skeleton.Joints[JointType.WristRight].Position.X);
            bool correcto = dif_codo_muneca < m_error && skeleton.Joints[JointType.WristRight].Position.Y < skeleton.Joints[JointType.ShoulderRight].Position.Y;

 

            return correcto;
        
        }

        private bool BrazoIzquierdoEncimaCabeza(Skeleton skeleton) {
            float long_brazo = System.Math.Abs(skeleton.Joints[JointType.Spine].Position.Y - skeleton.Joints[JointType.Head].Position.Y);
            bool correcto = skeleton.Joints[JointType.WristLeft].Position.Y > skeleton.Joints[JointType.Head].Position.Y &&
                skeleton.Joints[JointType.WristLeft].Position.X > skeleton.Joints[JointType.ElbowLeft].Position.X;

            return correcto;
        }

        private bool BrazoDerechoEncimaCabeza(Skeleton skeleton)
        {
            float long_brazo = System.Math.Abs(skeleton.Joints[JointType.Spine].Position.Y - skeleton.Joints[JointType.Head].Position.Y);
           bool correcto  = skeleton.Joints[JointType.WristRight].Position.Y > skeleton.Joints[JointType.Head].Position.Y &&
                skeleton.Joints[JointType.WristRight].Position.X < skeleton.Joints[JointType.ElbowRight].Position.X;

   
           return correcto;
        }

        private bool PiernasSeparadas(Skeleton skeleton) {
            // Cálculo de la longitud de la pierna
            float px = System.Math.Abs(skeleton.Joints[JointType.HipLeft].Position.X - skeleton.Joints[JointType.FootLeft].Position.X);
            float py = System.Math.Abs(skeleton.Joints[JointType.HipLeft].Position.Y - skeleton.Joints[JointType.FootLeft].Position.Y);
            float pz = System.Math.Abs(skeleton.Joints[JointType.HipLeft].Position.Z - skeleton.Joints[JointType.FootLeft].Position.Z);
            double longitud_pierna = System.Math.Sqrt((px * px) + (py * py) + (pz * pz));
            float suelo_cadera = System.Math.Abs(skeleton.Joints[JointType.HipCenter].Position.Y - skeleton.Joints[JointType.FootLeft].Position.Y);
            double pie_izq_a_centro = System.Math.Abs(skeleton.Joints[JointType.HipCenter].Position.X - skeleton.Joints[JointType.FootLeft].Position.X);
            double pie_der_a_centro = System.Math.Abs(skeleton.Joints[JointType.HipCenter].Position.X - skeleton.Joints[JointType.FootRight].Position.X);
            
            double ang_izq = System.Math.Atan(pie_izq_a_centro/suelo_cadera)*(180/System.Math.PI);
            double ang_der = System.Math.Atan(pie_der_a_centro/suelo_cadera)*(180/System.Math.PI);

            bool correcto = (ang_der+ang_izq) > this.angulo_separar-this.angulo_separar*m_error;


            
            return correcto;
        }

        private bool Ej0Correcto(Skeleton skeleton) {
            bool b1 = BrazoDerechoLevantado(skeleton);
            bool b2 = BrazoIzquierdoLevantado(skeleton);
            return ( b1 && b2 );
        }

        private bool Ej1Correcto(Skeleton skeleton) {
            bool b1 = BrazoDerechoPegadoCuerpo(skeleton);
            bool b2 = BrazoIzquierdoPegadoCuerpo(skeleton);
            return (b1 && b2);
        }


        private bool Ej2Correcto(Skeleton skeleton) {
            bool b1 = BrazoIzquierdoEncimaCabeza(skeleton);
            bool b2 = BrazoDerechoEncimaCabeza(skeleton);
            bool p = PiernasSeparadas(skeleton);
            return (b1 && b2 && p);
        }

        private bool Ej3Correcto(Skeleton skeleton) {
            bool p = PiernasEnElSuelo(skeleton);
            bool b1 = BrazoDerechoPegadoCuerpo(skeleton);
            bool b2 = BrazoIzquierdoPegadoCuerpo(skeleton);
            return (b1 && b2 && p);   
        }

        // Devuelve true si la posicion del ejercicio es correcta, false en otro caso
        private bool EjercicioCorrecto(int ejercicio, Skeleton skeleton) {
            bool correcto = false;
            switch(ejercicio){
                case (0):
                    correcto = Ej0Correcto(skeleton);
                         
                    break;
                case (1):
                    correcto = Ej1Correcto(skeleton);
                    break;
                case (2):
                    correcto = Ej2Correcto(skeleton);
                    break;
                case (3):
                    correcto = Ej3Correcto(skeleton);
                    break;

            }
            if (correcto) puntuacion += 300;
            else puntuacion--;
            return correcto;
        }

        private void DibujaPuntosAlcanzar(Skeleton skeleton, DrawingContext dc)
        {
            // Cálculo de la longitud del brazo
            float dx = System.Math.Abs(skeleton.Joints[JointType.ShoulderLeft].Position.X - skeleton.Joints[JointType.HandLeft].Position.X);
            float dy = System.Math.Abs(skeleton.Joints[JointType.ShoulderLeft].Position.Y - skeleton.Joints[JointType.HandLeft].Position.Y);
            float dz = System.Math.Abs(skeleton.Joints[JointType.ShoulderLeft].Position.Z - skeleton.Joints[JointType.HandLeft].Position.Z);
            double longitud_brazo = System.Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));

            // Cálculo de la longitud de la pierna
            float px = System.Math.Abs(skeleton.Joints[JointType.HipLeft].Position.X - skeleton.Joints[JointType.FootLeft].Position.X);
            float py = System.Math.Abs(skeleton.Joints[JointType.HipLeft].Position.Y - skeleton.Joints[JointType.FootLeft].Position.Y);
            float pz = System.Math.Abs(skeleton.Joints[JointType.HipLeft].Position.Z - skeleton.Joints[JointType.FootLeft].Position.Z);
            double longitud_pierna = System.Math.Sqrt((px * px) + (py * py) + (pz * pz));
            
            switch (this.ejercicio_actual) { 
                case 0:
                    var pos_brazo_izq = skeleton.Joints[JointType.ShoulderLeft].Position;
                    pos_brazo_izq.X = pos_brazo_izq.X - System.Convert.ToSingle(longitud_brazo);

                    var pos_brazo_der = skeleton.Joints[JointType.ShoulderRight].Position;
                    pos_brazo_der.X = pos_brazo_der.X + System.Convert.ToSingle(longitud_brazo);

                    dc.DrawEllipse(this.JointBrush, null, this.SkeletonPointToScreen(pos_brazo_izq), 20, 20);
                    dc.DrawEllipse(this.JointBrush, null, this.SkeletonPointToScreen(pos_brazo_der), 20, 20);
                    break;

                case 1:
                    pos_brazo_izq = skeleton.Joints[JointType.ShoulderLeft].Position;
                    pos_brazo_izq.Y = pos_brazo_izq.Y - System.Convert.ToSingle(longitud_brazo);

                    pos_brazo_der = skeleton.Joints[JointType.ShoulderRight].Position;
                    pos_brazo_der.Y = pos_brazo_der.Y - System.Convert.ToSingle(longitud_brazo);

                    dc.DrawEllipse(this.JointBrush, null, this.SkeletonPointToScreen(pos_brazo_izq), 20, 20);
                    dc.DrawEllipse(this.JointBrush, null, this.SkeletonPointToScreen(pos_brazo_der), 20, 20);
                    break;

                case 2:
                    pos_brazo_izq = skeleton.Joints[JointType.ShoulderLeft].Position;
                    pos_brazo_izq.Y = pos_brazo_izq.Y + System.Convert.ToSingle(longitud_brazo);

                    pos_brazo_der = skeleton.Joints[JointType.ShoulderRight].Position;
                    pos_brazo_der.Y = pos_brazo_der.Y + System.Convert.ToSingle(longitud_brazo);

                    dc.DrawEllipse(this.JointBrush, null, this.SkeletonPointToScreen(pos_brazo_izq), 20, 20);
                    dc.DrawEllipse(this.JointBrush, null, this.SkeletonPointToScreen(pos_brazo_der), 20, 20);

                    var pos_pierna_izq = skeleton.Joints[JointType.HipLeft].Position;
                    var pos_pierna_der = skeleton.Joints[JointType.HipRight].Position;
                    double r_x = System.Math.Abs(System.Math.Sin((2*System.Math.PI*this.angulo_separar)/360)*longitud_pierna);
                    double r_y = System.Math.Abs(System.Math.Cos((2 * System.Math.PI * this.angulo_separar) / 360) * longitud_pierna);

                    pos_pierna_izq.X = pos_pierna_izq.X - System.Convert.ToSingle(r_x);
                    pos_pierna_der.X = pos_pierna_der.X + System.Convert.ToSingle(r_x);

                    pos_pierna_der.Y  = pos_pierna_izq.Y = pos_pierna_der.Y - System.Convert.ToSingle(r_y);


                    dc.DrawEllipse(this.JointBrush, null, this.SkeletonPointToScreen(pos_pierna_izq), 20, 20);
                    dc.DrawEllipse(this.JointBrush, null, this.SkeletonPointToScreen(pos_pierna_der), 20, 20);

                    break;

                case 3:
                    pos_brazo_izq = skeleton.Joints[JointType.ShoulderLeft].Position;
                    pos_brazo_izq.Y = pos_brazo_izq.Y - System.Convert.ToSingle(longitud_brazo);

                    pos_brazo_der = skeleton.Joints[JointType.ShoulderRight].Position;
                    pos_brazo_der.Y = pos_brazo_der.Y - System.Convert.ToSingle(longitud_brazo);

                    pos_pierna_izq = skeleton.Joints[JointType.HipCenter].Position;
                    pos_pierna_der = skeleton.Joints[JointType.HipCenter].Position;

                    pos_pierna_der.Y = pos_pierna_izq.Y -= System.Convert.ToSingle(longitud_pierna);
                    pos_pierna_izq.X -= pos_pierna_izq.X * System.Convert.ToSingle(m_error);
                    pos_pierna_der.X += pos_pierna_der.X * System.Convert.ToSingle(m_error);


                    dc.DrawEllipse(this.JointBrush, null, this.SkeletonPointToScreen(pos_brazo_izq), 20, 20);
                    dc.DrawEllipse(this.JointBrush, null, this.SkeletonPointToScreen(pos_brazo_der), 20, 20);
                    dc.DrawEllipse(this.JointBrush, null, this.SkeletonPointToScreen(pos_pierna_izq), 20, 20);
                    dc.DrawEllipse(this.JointBrush, null, this.SkeletonPointToScreen(pos_pierna_der), 20, 20);



                    break;
            }
        }


        /// <summary>
        /// Draws a skeleton's bones and joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// 
        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext)
        {
            // Para cada uno de los posibles ejercicios
            this.campo_ejercicio.Content = this.nombre_ejercicio[ejercicio_actual];

            this.campo_repeticiones_restantes.Content = this.repeticiones_restantes;
            this.campo_series_restante.Content = this.series_restantes;
            switch (this.ejercicio_actual) {
                case (0):
                    if (EjercicioCorrecto(this.ejercicio_actual, skeleton)){
                        this.ejercicio_actual++;
                    }
                    break;


                case (1): 
                    if (EjercicioCorrecto(this.ejercicio_actual, skeleton)){
                        this.repeticiones_restantes--;
                        if (this.repeticiones_restantes == 0){
                            ejercicio_actual++;
                            this.repeticiones_restantes = repeticiones;
                        }
                        else{
                            this.ejercicio_actual--;
                        }
                    }
                    break;

                case (2):
 
                    if (EjercicioCorrecto(this.ejercicio_actual, skeleton)){

                        this.ejercicio_actual++;
                    }
                    break;

                case (3):
                    if (EjercicioCorrecto(this.ejercicio_actual, skeleton)){

                        this.repeticiones_restantes--;
                        if (this.repeticiones_restantes == 0){ // Si se han hecho todas las repeticiones
                            this.series_restantes--;
                            if (this.series_restantes == 0)
                            {
                                if (null != this.sensor)
                                {
                                    this.sensor.Stop();
                                }
                                Window2 ventana_final = new Window2();
                                ventana_final.ShowDialog();
                                //this.Close();

                            }
                            else {
                                this.ejercicio_actual = 0; // Empieza de nuevo el ejercicio
                                this.repeticiones_restantes = repeticiones;
                            }

                        }
                        else{ // Si aún quedan repeticiones por hacer
                            this.ejercicio_actual--;
                        }
                    }
                    break;
            }

            this.DibujaPuntosAlcanzar(skeleton, drawingContext);

        }
        

        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }

        /// <summary>
        /// Draws a bone line between two joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw bones from</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="jointType0">joint to start drawing from</param>
        /// <param name="jointType1">joint to end drawing at</param>
        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1)
        {
            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked ||
                joint1.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            // Don't draw if both points are inferred
            if (joint0.TrackingState == JointTrackingState.Inferred &&
                joint1.TrackingState == JointTrackingState.Inferred)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked){
            }

            drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
        }
    }
}