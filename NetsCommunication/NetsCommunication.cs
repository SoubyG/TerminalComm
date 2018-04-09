using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetsCommunication
{
    class NetsCommunication
    {
        private SerialPort _serialPort;
        private const int MAX_RETRY = 3;

        private const string ACK = "06";
        private const string NACK = "15";

        private const int RESPONSE_HEADERLENGTH = 3;
        private const int RESPONSE_ACKLENGTH = 1;

        private const int RESPONSE_ACKTIMEOUT = 2000;
        private const int RESPONSE_DATATIMEOUT = 120000;

        public NetsCommunication()
        {
            OpenSerialPort("COM4", 9600, StopBits.One, Parity.None, 8);
        }

        private void OpenSerialPort(string portName, int baudRate, StopBits stopBits, Parity parity, int dataBits)
        {
            try
            {
                // Open serial port here
                _serialPort = new SerialPort();
                _serialPort.PortName = portName;
                _serialPort.BaudRate = baudRate;
                _serialPort.StopBits = stopBits;
                _serialPort.Parity = parity;
                _serialPort.DataBits = dataBits;
                _serialPort.Open();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Open COM port fail:" + ex.Message + "\n");
            }
        }

        // cmd is hexstring
        private void WriteToSerialPort(byte[] cmd)
        {
            _serialPort.DiscardInBuffer();
            _serialPort.DiscardOutBuffer();
            _serialPort.Write(cmd, 0, cmd.Length);
        }

        // Known response length
        private byte[] ReadFromSerialPort(int timeOut, int responseLength)
        {
            Console.WriteLine("ReadFromSerialPort\n");
            var response = new byte[responseLength];
            try
            {
                int numBytesRead = 0;
                _serialPort.ReadTimeout = timeOut;
                Console.WriteLine("responseLength: "+responseLength+"\n");
                while (numBytesRead != responseLength)
                {
                    Console.WriteLine(numBytesRead);
                    numBytesRead += _serialPort.Read(response, numBytesRead, responseLength - numBytesRead);
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine("Serial port read exception:" + ex.Message + "\n");
                response = null;
            }
            
            return response;
        }

        public bool ExecuteCommand(byte[] cmd)
        {
            byte retryCount = 0;
            bool ackStatus = false;
            bool responseStatus = false;
            while ((retryCount < MAX_RETRY) && ackStatus == false)
            {
                Console.WriteLine("Akstatus retry:" + retryCount);
                retryCount++;
                WriteToSerialPort(cmd);
                var ackMessage = ReadFromSerialPort(RESPONSE_ACKTIMEOUT,RESPONSE_ACKLENGTH);
                // 2.2.2.2
                if(ackMessage != null)
                {
                    // 2.2.2.1
                    if(BitConverter.ToString(ackMessage).Replace("-", "") == ACK)
                    {
                        ackStatus = true;
                    }
                }               
            }
            if(ackStatus == false)
            {
                return false; // Transaction failure
            }
            retryCount = 0;
            while ((retryCount < MAX_RETRY) && responseStatus == false)
            {
                retryCount++;
                Console.WriteLine("responseStatus retry:" + retryCount);
                var responseHeader = ReadFromSerialPort(RESPONSE_DATATIMEOUT,RESPONSE_HEADERLENGTH);
                if (responseHeader != null)
                {
                    var responseDataLength = Int16.Parse(BitConverter.ToString(responseHeader,1).Replace("-",""), 0);
                    var responseData = ReadFromSerialPort(RESPONSE_DATATIMEOUT, responseDataLength + 2);
                    var response = responseHeader.Concat(responseData).ToArray();
                    if(response != null)
                    {
                        if (ValidateResponse(response))
                        {
                            responseStatus = true;
                            // Send ACK
                            // 2.2.1
                            WriteToSerialPort(new byte[] { 0x06 });
                        }
                        else
                        {
                            // 2.2.2.3
                            // Send NACK
                            WriteToSerialPort(new byte[] { 0x15 });
                        }
                    }
                    else
                    {
                        // 2.2.2.5
                        retryCount = 3;
                    }
                }
            }
            if(responseStatus == false)
            {
                return false;
            }
            return true;
           
        }

        private bool ValidateResponse(byte[] readData)
        {
         
            var dataForCRCComputation = new byte[readData.Length - 2];

            Array.Copy(readData, 1, dataForCRCComputation, 0, readData.Length - 2);
            if (ComputeCRC(dataForCRCComputation) == readData[readData.Length - 1])
                return true;
            else
                return false;
        }


        private byte ComputeCRC(byte[] data)
        {
            byte LRC = 0;
            for (int i = 0; i < data.Length; i++)
            {
                LRC ^= data[i];
            }
            return LRC;
        }

        ~NetsCommunication()
        {
            _serialPort.Close();
        }
    }
}
