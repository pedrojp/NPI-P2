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
        /// Ángulo en el que es necesario levanta la pierna
        private int angulo_levantar = 60;
        /// Ángulo en el que ambas piernas deben separarse
        private int angulo_separar = 40;
        /// Ángulo general
        private double angulo_general = 0;

        /// Series, repeticiones y margen de error del ejercicio
        public static int series = 0, repeticiones;
        public static double m_error;

        /// Variables a mostrar en la ventana principal
        private int series_restantes, repeticiones_restantes;
        int ejercicio_actual; // Levantar pierna derecha -> 0, levantar pierna izquierda-> 1, piernas abiertas brazos arriba -> 2, piernas cerradas brazos abajo -> 3
        bool vuelta_ejericicio = false;
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
        private readonly Brush trackedJointBrushVerde = Brushes.Green;
        private readonly Brush trackedJointAmarillo = Brushes.Yellow;
        private readonly Brush trackedJointTurquesa = Brushes.Turquoise;



        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Objetos pen para dibujar el esqueleto 
        /// </summary>
        private readonly Pen trackedBonePenVerde = new Pen(Brushes.Green, 6);
        private readonly Pen trackedBonePenRojo = new Pen(Brushes.Red, 6);

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
            this.nombre_ejercicio[0] = "Brazos en cruz. Pierna derecha levantada ";
            this.nombre_ejercicio[1] = "Piernas apoyadas en el suelo. Brazos pegados al cuerpo";
            this.nombre_ejercicio[2] = "Brazos en cruz. Pierna izquierda levantada ";
            this.nombre_ejercicio[3] = "Brazos por encima de la cabeza. Piernas abiertas ";
            this.nombre_ejercicio[4] = "Piernas cerradas. Brazos relajados.";
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
            Image.Source = this.imageSource;

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
                dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

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

            // This is the bitmap we'll display on-screen
            this.colorBitmap = new WriteableBitmap(this.sensor.ColorStream.FrameWidth, this.sensor.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

            // Set the image we display to point to the bitmap where we'll put the image data
        }


        private bool BrazoIzquierdoLevantado(Skeleton skeleton)
        {
            // Si el brazo derecho se encuentra por encima de la cabeza
            return (skeleton.Joints[JointType.ElbowLeft].Position.Y >= skeleton.Joints[JointType.ShoulderLeft].Position.Y) &&
                                    (skeleton.Joints[JointType.WristLeft].Position.Y >= skeleton.Joints[JointType.ShoulderLeft].Position.Y) &&
                                    (skeleton.Joints[JointType.WristLeft].Position.Y < skeleton.Joints[JointType.Head].Position.Y);
        }

        private bool BrazoDerechoLevantado(Skeleton skeleton)
        {
            // Si el brazo izquierdo se encuentra por encima de la cabeza
            return (skeleton.Joints[JointType.ElbowRight].Position.Y >= skeleton.Joints[JointType.ShoulderRight].Position.Y) &&
                                  (skeleton.Joints[JointType.WristRight].Position.Y >= skeleton.Joints[JointType.ShoulderRight].Position.Y) &&
                                  (skeleton.Joints[JointType.WristRight].Position.Y < skeleton.Joints[JointType.Head].Position.Y);
        }

        private bool PiernaDerechaLevantada(Skeleton skeleton)
        {
            // Longitud en y de la pierna izquierda
            double dy_pierna_izquierda = System.Math.Abs(skeleton.Joints[JointType.HipLeft].Position.Y - skeleton.Joints[JointType.AnkleLeft].Position.Y);

            // Longitud en z entre los tobillos
            double dz_tobillos = System.Math.Abs(skeleton.Joints[JointType.AnkleLeft].Position.Z - skeleton.Joints[JointType.AnkleRight].Position.Z);
            this.angulo_general = (dz_tobillos * 90) / dy_pierna_izquierda;
            System.Console.WriteLine("{0}", this.angulo_general);
            // En el caso de que se forme un ángulo que se tome como correcto con la pierna derecha, devuelve true
            return ((((dz_tobillos * 90) / dy_pierna_izquierda) > (angulo_levantar - m_error*angulo_levantar ) && (((dz_tobillos * 90) / dy_pierna_izquierda) < angulo_levantar + m_error*angulo_levantar)));
        }

        private bool PiernaIzquierdaLevantada(Skeleton skeleton) 
        {
            // Longitud en y de la pierna derecha
            double dy_pierna_derecha = System.Math.Abs(skeleton.Joints[JointType.HipRight].Position.Y - skeleton.Joints[JointType.AnkleRight].Position.Y);

            // Longitud en z entre los tobillos
            double dz_tobillos = System.Math.Abs(skeleton.Joints[JointType.AnkleLeft].Position.Z - skeleton.Joints[JointType.AnkleRight].Position.Z);
            this.angulo_general = (dz_tobillos * 90) / dy_pierna_derecha;
            // En el caso de que se forme un ángulo que se tome como correcto con la pierna derecha, devuelve true
            return ((((dz_tobillos * 90) / dy_pierna_derecha) > angulo_levantar - m_error*angulo_levantar) && (((dz_tobillos * 90) / dy_pierna_derecha) < angulo_levantar +  m_error*angulo_levantar));
        
        }

        private bool PiernasEnElSuelo(Skeleton skeleton) {
            double dif = System.Math.Abs(skeleton.Joints[JointType.FootLeft].Position.Y - skeleton.Joints[JointType.FootRight].Position.Y);
            return dif < m_error;

        }

        private bool BrazoIzquierdoPegadoCuerpo(Skeleton skeleton) {
            double dif_codo_muneca = System.Math.Abs(skeleton.Joints[JointType.ElbowLeft].Position.X - skeleton.Joints[JointType.WristLeft].Position.X);
            return dif_codo_muneca < m_error && skeleton.Joints[JointType.WristLeft].Position.Y < skeleton.Joints[JointType.ShoulderLeft].Position.Y;
        }

        private bool BrazoDerechoPegadoCuerpo(Skeleton skeleton)
        {
            double dif_codo_muneca = System.Math.Abs(skeleton.Joints[JointType.ElbowRight].Position.X - skeleton.Joints[JointType.WristRight].Position.X);
            return dif_codo_muneca < m_error && skeleton.Joints[JointType.WristRight].Position.Y < skeleton.Joints[JointType.ShoulderRight].Position.Y;
        }

        private bool BrazoIzquierdoEncimaCabeza(Skeleton skeleton) {
            return skeleton.Joints[JointType.WristLeft].Position.Y > skeleton.Joints[JointType.Head].Position.Y &&
                skeleton.Joints[JointType.WristLeft].Position.X > skeleton.Joints[JointType.ElbowLeft].Position.X;
        }

        private bool BrazoDerechoEncimaCabeza(Skeleton skeleton)
        {
            return skeleton.Joints[JointType.WristRight].Position.Y > skeleton.Joints[JointType.Head].Position.Y &&
                skeleton.Joints[JointType.WristRight].Position.X < skeleton.Joints[JointType.ElbowRight].Position.X;
        }

        private bool PiernasSeparadas(Skeleton skeleton) {
            double suelo_cadera = System.Math.Abs(skeleton.Joints[JointType.HipCenter].Position.Y - skeleton.Joints[JointType.AnkleLeft].Position.Y);
            double pie_izq_a_centro = System.Math.Abs(skeleton.Joints[JointType.HipCenter].Position.X - skeleton.Joints[JointType.AnkleLeft].Position.X);
            double pie_der_a_centro = System.Math.Abs(skeleton.Joints[JointType.HipCenter].Position.X - skeleton.Joints[JointType.AnkleRight].Position.X);
            
            double ang_izq = System.Math.Atan(pie_izq_a_centro/suelo_cadera)*(180/System.Math.PI);
            double ang_der = System.Math.Atan(pie_der_a_centro/suelo_cadera)*(180/System.Math.PI);

            this.angulo_general = ang_der + ang_izq;

            return (ang_der+ang_izq) < this.angulo_separar+this.angulo_separar*m_error && (ang_der+ang_izq) > this.angulo_separar-this.angulo_separar*m_error;

        }

        // Devuelve true si la posicion del ejercicio es correcta, false en otro caso
        private bool EjercicioCorrecto(int ejercicio, Skeleton skeleton) {
            bool correcto = false;
            switch(ejercicio){
                case (0):
                    correcto = BrazoIzquierdoLevantado(skeleton) 
                        && BrazoIzquierdoLevantado(skeleton) && PiernaDerechaLevantada(skeleton);
                    break;
                case (1):
                    correcto = PiernasEnElSuelo(skeleton) && BrazoDerechoPegadoCuerpo(skeleton) && BrazoIzquierdoPegadoCuerpo(skeleton);
                    break;
                case (2):
                    correcto = BrazoIzquierdoLevantado(skeleton) && BrazoIzquierdoLevantado(skeleton) && PiernaIzquierdaLevantada(skeleton);
                    break;
                case (3):
                    correcto = BrazoIzquierdoEncimaCabeza(skeleton) && BrazoDerechoEncimaCabeza(skeleton) && PiernasSeparadas(skeleton);
                    break;
                case (4):
                    correcto = PiernasEnElSuelo(skeleton) && BrazoDerechoPegadoCuerpo(skeleton) && BrazoIzquierdoPegadoCuerpo(skeleton);
                    break;

            }
            return correcto;
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
            if (this.ejercicio_actual == 0 || this.ejercicio_actual == 2 || this.ejercicio_actual == 3)
                this.campo_ejercicio.Content = this.nombre_ejercicio[ejercicio_actual] + this.angulo_levantar + " grados aproximadamente (actualmente " + System.Convert.ToInt16(this.angulo_general) + " grados)";
            else
                this.campo_ejercicio.Content = this.nombre_ejercicio[ejercicio_actual];

            this.campo_repeticiones_restantes.Content = this.repeticiones_restantes;
            this.campo_series_restante.Content = this.series_restantes;
            switch (this.ejercicio_actual) {
                case (0):
                    if (EjercicioCorrecto(this.ejercicio_actual, skeleton)){
                        this.ejercicio_actual++;
                        this.vuelta_ejericicio = false;

                    }
                    break;

                case (1):
                    this.angulo_general = 0;
                    if (EjercicioCorrecto(this.ejercicio_actual, skeleton))
                    {
                        if (!vuelta_ejericicio) this.ejercicio_actual++;
                        else this.ejercicio_actual--;
                    }
                    break;

                case (2): 
                    if (EjercicioCorrecto(this.ejercicio_actual, skeleton)){
                        this.repeticiones_restantes--;
                        if (this.repeticiones_restantes == 0){
                            ejercicio_actual++;
                            this.repeticiones_restantes = repeticiones;
                        }
                        else{
                            this.ejercicio_actual--;
                            this.vuelta_ejericicio = true;
                        }
                    }
                    break;

                case (3):
                    System.Console.WriteLine("BI:{0} BD:{1} P:{2}" , BrazoIzquierdoEncimaCabeza(skeleton), BrazoDerechoEncimaCabeza(skeleton), PiernasSeparadas(skeleton));
                    if (EjercicioCorrecto(this.ejercicio_actual, skeleton)){
                        System.Console.WriteLine("Ejercicio CORRECTO");
                        this.ejercicio_actual++;
                    }
                    break;

                case (4):
                    if (EjercicioCorrecto(this.ejercicio_actual, skeleton)){

                        this.repeticiones_restantes--;
                        if (this.repeticiones_restantes == 0){ // Si se han hecho todas las repeticiones
                            this.series_restantes--;
                            if (this.series_restantes == 0)
                            {
                                System.Console.WriteLine("FIN DEL EJERCICIO");
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


            // Render Torso
            this.DrawBone(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.Spine);
            this.DrawBone(skeleton, drawingContext, JointType.Spine, JointType.HipCenter);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipLeft);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipRight);

            // Left Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
            this.DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);

            // Right Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight);
            this.DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight);

            // Left Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft);
            this.DrawBone(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft);

            // Right Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipRight, JointType.KneeRight);
            this.DrawBone(skeleton, drawingContext, JointType.KneeRight, JointType.AnkleRight);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleRight, JointType.FootRight);

            // Render Joints
            foreach (Joint joint in skeleton.Joints)
            {
                Brush drawBrush = null;

                if (joint.TrackingState == JointTrackingState.Tracked)
                {

                   drawBrush = this.trackedJointBrushVerde;
                }

                else if (joint.TrackingState == JointTrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, this.SkeletonPointToScreen(joint.Position), JointThickness, JointThickness);
                }
            }
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

                        drawPen = this.trackedBonePenVerde;
            }

            drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
        }
    }
}