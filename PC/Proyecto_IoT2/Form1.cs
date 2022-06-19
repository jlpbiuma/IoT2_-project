using System;
using System.IO.Ports;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Forms;


namespace Proyecto_IoT2
{
    public partial class Form1 : Form
    {
        static SerialPort puerto = new SerialPort();
        string[] ports = SerialPort.GetPortNames();
        string selectedPort = "";
        public delegate void MyDelegate(string text);
        const int longitudTrama = 15;
        static float[] arrayDatosDouble = new float[15];

        public Form1()
        {
            //Para poder escuchar por el puerto serie y poder llamar a la textbox
            CheckForIllegalCrossThreadCalls = false; 
            InitializeComponent();
            Inicializacion();
        }
        public void Inicializacion()
        {
            comboBox1.Items.AddRange(ports);
        }
        
        private void ConfigurarComunicaciones()
        {
            ConfigurarPuerto();
        }

        private void ConfigurarPuerto()
        {
            puerto.PortName = selectedPort;
            puerto.BaudRate = 9600;
            puerto.Parity = Parity.None;
            puerto.DataBits = 8;
            puerto.StopBits = StopBits.One;
            puerto.ReadBufferSize = 4096;
            puerto.WriteBufferSize = 4096;
            puerto.ReadTimeout = 500;
            puerto.WriteTimeout = 500;
            puerto.RtsEnable = false;
            puerto.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
        }

        public void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            Thread.Sleep(500);
            SerialPort sp = (SerialPort)sender;
            string indata = sp.ReadExisting().Replace("\n", ""); // Obtiene los datos y quita "\n"
            int final = indata.IndexOf(" array");
            indata = indata.Substring(0, final);
            string texto = "";
            string[] array = indata.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries).ToArray(); // Transformacion de List a Array, quitar espacios 
            // El array que conocemos es de 12 elementos.
            if (array.Length != longitudTrama)
            {
                return;
            }
            string datosString = "";
            RAW2DoubleAndString(array, out arrayDatosDouble, out datosString);
            MyDelegate d = new MyDelegate(changeText1);
            d.Invoke(datosString);
        }

        private static void RAW2DoubleAndString(string[] array, out float[] arrayDatosDouble, out string datosString)
        {
            float[] arrayDatosDoubleInter = new float[longitudTrama];
            string datosStringInter = "";
            for (int i = 0; i < longitudTrama; i++)
            {
                string actualString = array[i];
                int indexOfE = actualString.IndexOf("e");
                string numberSubString = actualString.Substring(0, indexOfE - 1);
                float value = Convert.ToSingle(numberSubString, CultureInfo.InvariantCulture);
                int potencia = int.Parse(actualString.Substring(indexOfE + 1));
                arrayDatosDoubleInter[i] = value * (float)Math.Pow(10,potencia);
                datosStringInter += arrayDatosDoubleInter[i].ToString() + "\r\n";
            }
            arrayDatosDouble = arrayDatosDoubleInter;
            datosString = datosStringInter;
        }

        public void changeText1(string datos)
        {
            //textBox1.Text += datos + "\r\n";
            //textBox1.SelectionStart = textBox1.TextLength;
            //textBox1.ScrollToCaret();
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            selectedPort = ports[comboBox1.SelectedIndex];
            if (selectedPort == "")
            {
                MessageBox.Show("Seleccione el puerto donde se inicia la conexión vía Bluetooth");
                return;
            }
            ConfigurarComunicaciones();
            puerto.Open();
        }

        private void Escribir(string data)
        {
            puerto.WriteLine(data);
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox3_TextChanged(object sender, EventArgs e)
        {

        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private float[] ExtractAccelVector(float[] arrayDatos)
        {
            float[] arrayAccelVector = new float[3];
            for (int i = 0; i < 3; i++)
            {
                arrayAccelVector[i] = arrayDatos[i];
            }
            return arrayAccelVector;
        }
        private float[] ExtractMagVector(float[] arrayDatos)
        {
            float[] arrayAccelVector = new float[3];
            for (int i = 3; i < 6; i++)
            {
                arrayAccelVector[i - 3] = arrayDatos[i];
            }
            return arrayAccelVector;
        }
        private float[] ExtractDronPositionVector(float[] arrayDatos)
        {
            float[] arrayDron = new float[3];
            for (int i = 12; i < 15; i++)
            {
                arrayDron[i - 12] = arrayDatos[i];
            }
            return arrayDron;
        }
        private float[] ExtractGPSPositionVector(float[] arrayDatos)
        {
            float[] arrayGPSPosition = new float[2];
            for (int i = 9; i < 11; i++)
            {
                arrayGPSPosition[i - 9] = arrayDatos[i];
            }
            return arrayGPSPosition;
        }

        private float[] CalcularMatrizTransformacion(float[] v1, float[] v2)
        {
            // v1 = accelerometer
            // v2 = magnetometer
            float[] R = new float[v1.Length * 3];
            float[] R1 = NormalizeVector(CrossProduct(CrossProduct(v1,v2),v1)); // crossProduct(crossProduct(A,M),A);
            float[] R2 = NormalizeVector(CrossProduct(v1, v2));
            float[] R3 = NormalizeVector(v1);
            for (int i = 0; i < 9; i++)
            {
                switch(i)
                {
                    case < 3:
                        R[i] = R1[i];
                        break;
                    case < 6:
                        R[i] = R2[3 - i];
                        break;
                    case < 9:
                        R[i] = R3[6 - i];
                        break;
                }
            }
            return R;
        }

        private float[] CrossProduct(float[] v1, float[] v2)
        {
            float[] vResult = new float[v1.Length];
            vResult[0] = v1[1] * v2[2] - v1[2] * v2[1];
            vResult[1] = v1[0] * v2[2] - v1[2] * v2[0];
            vResult[2] = v1[0] * v2[1] - v1[1] * v2[0];
            return vResult;
        }
        private float[] NormalizeVector(float[] vIn)
        {
            int longitud = vIn.Length;
            float[] vOut = new float[vIn.Length];
            float denominador = CalcularModuloVector(vIn);
            for (int i = 0; i < longitud; i++)
            {
                vOut[i] = vIn[i] / denominador;
            }
            return vOut;
        }
        private float CalcularModuloVector(float[] vIn)
        {
            int longitud = vIn.Length;
            float valor = 0F;
            for (int i = 0; i < longitud; i++)
            {
                valor += vIn[i] * vIn[i];
            }
            return (float)Math.Sqrt(valor);
        }
        private string Array2String(float[] Array)
        {
            int longitud = Array.Length;
            string result = "";
            for (int i = 0; i < longitud; i++)
            {
                result += Array[i] + "\r\n";
            }
            return result;
        }
        private float[] RestaArray(float[] v1, float[] v2)
        {
            int longitud = v1.Length;
            float[] result = new float[v1.Length];
            for (int i = 0; i < longitud; i++)
            {
                result[i] = v1[i] - v2[i];
            }
            return result;
        }
        private float CalculatePitch(float[] arrayDronPosition, float[] arrayGPSPosition)
        {
            float[] relativePositionDron = RestaArray(arrayDronPosition, arrayGPSPosition);
            double pitch = 0F;
            pitch = Math.Atan(relativePositionDron[3] / Math.Sqrt(Math.Pow(relativePositionDron[0],2) + Math.Pow(relativePositionDron[1], 2)));
            return (float)Math.Round(pitch, 2);
        }
        private float CalculateYaw(float[] arrayDronPosition, float[] arrayGPSPosition)
        {
            float[] relativePositionDron = RestaArray(arrayDronPosition,arrayGPSPosition);
            double yaw = 0F;
            yaw = Math.Atan((double)(relativePositionDron[1] / relativePositionDron[0]));
            return (float)Math.Round(yaw, 2);
        }
        private void RecalculateTransformationMatrixAndSend()
        {
            float[] arrayAccelVector = ExtractAccelVector(arrayDatosDouble);
            float[] arrayMagVector = ExtractMagVector(arrayDatosDouble);
            float[] arrayGPSPosition = ExtractGPSPositionVector(arrayDatosDouble);
            float[] arrayDronPositionRAW = ExtractDronPositionVector(arrayDatosDouble);
            float[] R = CalcularMatrizTransformacion(arrayAccelVector, arrayMagVector);
            // Transformación de coordenadas relativas a absolutas:
            float[] arrayDronPositionTransformed = Rel2Abs(R, arrayDronPositionRAW);
            // Cálculo de los ángulos de GIMBALL
            float yaw = CalculateYaw(arrayDronPositionTransformed, arrayGPSPosition);
            float pitch = CalculatePitch(arrayDronPositionTransformed, arrayGPSPosition);
            string trama = yaw.ToString() + " " + pitch.ToString();
            // Mandar pitch y yaw:
            Escribir("Normal: " + trama);
        }
        private float[] Rel2Abs(float[] R, float[] arrayDronPositionRAW)
        {
            int longitud = arrayDronPositionRAW.Length;
            float[] vOut = new float[arrayDronPositionRAW.Length];
            for (int j = 0; j < longitud; j++)
            {
                float valor = 0;
                for (int i = 0; i < 3; i++)
                {
                    valor += arrayDronPositionRAW[i] * R[i + 3 * j];
                }
                vOut[j] = valor;
            }
            return vOut;
        }
        private void writeInTextBoxes()
        {
            // Aceleracion
            textBox3.Text = arrayDatosDouble[0].ToString();
            textBox4.Text = arrayDatosDouble[1].ToString();
            textBox5.Text = arrayDatosDouble[2].ToString();
            // Magnetometro
            textBox8.Text = arrayDatosDouble[3].ToString();
            textBox7.Text = arrayDatosDouble[4].ToString();
            textBox6.Text = arrayDatosDouble[5].ToString();
            // Gyro
            textBox11.Text = arrayDatosDouble[6].ToString();
            textBox10.Text = arrayDatosDouble[7].ToString();
            textBox9.Text = arrayDatosDouble[8].ToString();
            // Pitch y yaw IUMA
            textBox12.Text = arrayDatosDouble[9].ToString();
            textBox13.Text = arrayDatosDouble[10].ToString();
            // Dron position
            textBox14.Text = arrayDatosDouble[0].ToString();
            textBox15.Text = arrayDatosDouble[1].ToString();
            textBox16.Text = arrayDatosDouble[2].ToString();
            // GPS position
            textBox17.Text = arrayDatosDouble[0].ToString();
            textBox18.Text = arrayDatosDouble[1].ToString();
            textBox19.Text = arrayDatosDouble[2].ToString();
            // GIMBALL
            textBox21.Text = arrayDatosDouble[0].ToString();
            textBox20.Text = arrayDatosDouble[0].ToString();
        }
        
        private void timer1_Tick(object sender, EventArgs e)
        {
            writeInTextBoxes();
            RecalculateTransformationMatrixAndSend();
        }

        private void textBox14_TextChanged(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            Escribir(textBox14.Text);
        }

        private void Calibrate(object sender, EventArgs e)
        {
            Escribir("Calibrar");
        }

        private void ManualChangeButton_Click(object sender, EventArgs e)
        {
            string pitchManual = textBox21.Text;
            string yawManual = textBox20.Text;
            Escribir("Manual: " + pitchManual + " " + yawManual);
        }

        private void HomeButton_Click(object sender, EventArgs e)
        {
            Escribir("Home");
        }
    }
}