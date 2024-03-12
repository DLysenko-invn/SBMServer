from __future__ import annotations

import time
import threading

import serial
from serial.tools import list_ports

from typing import Optional,List,Callable




class ISDPFrame:


    MAGIC0 = 0x55
    MAGIC1 = 0xAA

    SYNC0 = 0
    SYNC1 = 1
    SIZE =  2    
    PAYLOAD = 10
    DONE = 999

    def __init__(self):
        self.stage = ISDPFrame.SYNC0
        self.data = None
        self.protocolerrordetected = False
        self.nextreadsize = 1


    @property
    def MaxRead(self)->int: 
        return self.nextreadsize

    @property
    def IsError(self):
        return self.protocolerrordetected


    def Put(self,d:bytearray)->int:# -> bytes consumed
        if (d==None) or (len(d)==0):
            return
        if   (self.stage==ISDPFrame.SYNC0):
            if (d[0]==ISDPFrame.MAGIC0):
                self.stage=ISDPFrame.SYNC1
                self.nextreadsize = 1
                return 1
        elif (self.stage==ISDPFrame.SYNC1):
            if (d[0]==ISDPFrame.MAGIC1):
                self.stage=ISDPFrame.SIZE
                self.nextreadsize = 2
                self.payloadsize = 0
                return 1
            self.stage=ISDPFrame.SYNC0
            self.nextreadsize = 1
            self.protocolerrordetected = True
            return 0
        elif (self.stage==ISDPFrame.SIZE):
            assert len(d) == 2
            self.stage=ISDPFrame.PAYLOAD
            self.nextreadsize = d[0] + d[1]*0x100
            return 2
        elif (self.stage==ISDPFrame.PAYLOAD):
            n = len(d)
            assert n<=self.nextreadsize
            self.nextreadsize -= n
            if (self.data==None):
                self.data = d
            else:
                self.data += d
            if (self.nextreadsize==0):
                self.stage=ISDPFrame.DONE
            return n
        elif (self.stage==ISDPFrame.DONE):
            assert False, 'Writing after frame end'
        

        assert False, 'Wrong FSA stage'





    @property
    def IsFinished(self)->bool:
        return self.stage == ISDPFrame.DONE

    def GetByteAt(self,index:int)->Optional[int]:
        if (not self.IsFinished):
            return None
        if (self.data == None) or (index>=len(self.data)):
            return None
        return self.data[index]

    def GetBytesAfter(self,index:int)->Optional[bytearray]:
        if (not self.IsFinished):
            return None
        if (self.data == None) or (index>len(self.data)):
            return None
        return self.data[index:]


    def ToBytes(self)->bytearray:
        payload = bytearray() if (self.data==None) else self.data
        size = len(payload)
        data = bytearray(4+size)
        data[0] = ISDPFrame.MAGIC0
        data[1] = ISDPFrame.MAGIC1
        data[2] = (size) & 0xFF 
        data[3] = (size >> 8) & 0xFF 
        data[4:4+size] = payload
        return data




    @staticmethod
    def FromPayload(payload:bytearray)->ISDPFrame:
        assert isinstance(payload, (bytearray))
        frame = ISDPFrame()
        frame.stage = ISDPFrame.DONE
        frame.data = payload
        return frame





class ISDPIO:

    def Open(self,port:str,baud:int,timeout:int):
        pass

    def Read(self,size:int)->bytearray:
        return None

    def Write(self,data:bytearray):
        pass

    def Close(self):
        pass

    def IsConnected(self):
        return False


class ISDPIOSerial(ISDPIO):

    def __init__(self):
        self.com = None

    def Open(self,port:str,baud:int,readtimeout:int):
        self.com = serial.Serial(port, baud, timeout=readtimeout)

    def Read(self,size:int)->bytearray:
        return self.com.read(size)

    def Write(self,data:bytearray):
        return self.com.write(data)

    def Close(self):
        self.com.close()
        self.com = None

    def IsConnected(self):
        return self.com!=None



class ISDPTransport:
    
    DEFAULT_BAUDRATE = 2000000
    READ_TIMEOUT_SEC = 1
    THREAD_WAIT_SEC = 3

    MAX_FRAMES_TO_STORE = 1000
    DEFAULT_TALKTIMEOUT_SEC = 0.5
    
    def __init__(self,portname:str,failcallbackfunc:Callable=None):
        self.port = portname
        self.baud = ISDPTransport.DEFAULT_BAUDRATE
        self.io = None
        self.readtimeout = ISDPTransport.READ_TIMEOUT_SEC
        self.thread = None
        self.startevent = threading.Event()
        self.stopevent = threading.Event()
        self.isstopthread = False
        self.currframe = None
        self.mutex = threading.Lock()
        #self.debugprint_mutex = threading.Lock()
        self.buffer = []
        self.bufferoverflowflag = False
        self.responce = None
        self.hasevents = threading.Event()
        self.hasresponce = threading.Event()
        self.failcallback = failcallbackfunc if failcallbackfunc!=None else ISDPTransport.DefaultFailCallback
        


    @staticmethod
    def DefaultFailCallback(message:str):
        raise Exception(self.port+' error: '+message)


    def FatalError(self,message:str):
        print("Fatal error:"+message)
        self.Close()
        self.failcallback(message)


    @property
    def IsConnected(self):
        return False if self.io==None else self.io.IsConnected()


    def Open(self,io:ISDPIO=None):
        self.io = ISDPIOSerial() if (io==None) else io
        try:
            self.io.Open(self.port, self.baud, self.readtimeout)
        except Exception as e:
            self.io = None
            self.FatalError(str(e))
            return

        self.startevent.clear()
        self.stopevent.clear()
        self.isstopthread = False
        self.thread = threading.Thread(target = self.PortReadThread)
        self.thread.start()
        self.startevent.wait()
        


    def Close(self):
        if (self.thread != None):
            self.isstopthread = True
            self.stopevent.wait(self.THREAD_WAIT_SEC)
            self.thread = None

        if (self.io!=None):
            io = self.io
            self.io=None
            try:
                io.Close() 
            except:
                pass

            
        



    def ProcessCurrentFrame(self):
        self.mutex.acquire()

        cmd = ISDPCmd.FromFrame(self.currframe)

        if (cmd.GroupID!=ISDPCmd.GROUPID):
            self.FatalError('Wrong protocol version "'+str(cmd.GroupID)+'"')
        else:
            if (cmd.ETYPE == ISDPCmd.ETYPE_ASYNC):
                self.buffer.append(cmd)
                self.hasevents.set()
                while (len(self.buffer)>ISDPTransport.MAX_FRAMES_TO_STORE):
                    self.buffer.pop(0)
                    self.bufferoverflowflag = True
            elif (cmd.ETYPE == ISDPCmd.ETYPE_RESP):
                self.responce = cmd
                self.hasresponce.set()
            else:
                self.FatalError('Wrong ETYPE "'+str(cmd.ETYPE)+'"')

        self.mutex.release()



    def GetResponce(self)->ISDPCmd:
        cmd = None
        self.mutex.acquire()
        cmd = self.responce
        self.responce = None
        self.hasresponce.clear()
        self.mutex.release()
        return cmd


    def GetEvent(self)->ISDPCmd:
        cmd = None
        self.mutex.acquire()
        if (len(self.buffer)>0):
            cmd = self.buffer.pop(0)
        if (len(self.buffer)==0):
            self.hasevents.clear()
        self.mutex.release()
        return cmd


    def GetAllEvents(self)->ISDPCmd:
        cmds = None
        self.mutex.acquire()
        cmds = self.buffer
        self.buffer = []
        self.bufferoverflowflag = False      
        self.hasevents.clear()
        self.mutex.release()
        return cmds


    def FlushEvents(self):
        self.GetAllEvents()

    def FlushResponce(self):
        self.GetResponce()

    def DebugPrintBytes(self,prefix:str,bytes:bytearray):
        pass
        #self.debugprint_mutex.acquire()
        #text = prefix + ''.join(format(x, '02X') for x in bytes)+' ('+str(len(bytes))+')\r'
        #print(text, flush=True, sep='' )
        #self.debugprint_mutex.release()

    def Write(self,data:bytearray)->bool:
        if (not self.IsConnected):
            return False
        try:
            self.io.Write(data)
            self.DebugPrintBytes("W: ",data)
        except Exception as e:
            self.FatalError(str(e))
            return False
        return True

    def PortRead(self)->bool:

        if (self.currframe==None):
            self.currframe = ISDPFrame()

        try:
            data = self.io.Read(self.currframe.MaxRead)
        except Exception as e:
            self.FatalError(str(e))
            return False

        if (data==None) or (len(data)==0):
            return True

        self.DebugPrintBytes("R: ",data)
        bytesconsumed = self.currframe.Put(data)


        #todo: cut tail and Put it again
        assert bytesconsumed==len(data)


        if (self.currframe.IsError):
            self.FatalError(str(e))
            return False


        if (self.currframe.IsFinished):
            self.ProcessCurrentFrame()
            self.currframe = None

        return True



    def PortReadThread(self):
        self.startevent.set()
        while (not self.isstopthread):
            if (not self.PortRead()):
                break
        self.stopevent.set()



    def WaitResponce(self,timeout_sec:float)->bool:
        return self.hasresponce.wait(timeout_sec)

    def WaitEvent(self,timeout_sec:float)->bool:
        return self.hasevents.wait(timeout_sec)



    def MakeATalk(self, cmd:ISDPCmd, timeout_sec:float=DEFAULT_TALKTIMEOUT_SEC)->ISDPCmd:
        if (not self.IsConnected):
            return None
        self.FlushResponce()
        if (not self.Write(cmd.Frame.ToBytes())):
            return None
        self.WaitResponce(timeout_sec)
        responce = self.GetResponce()
        return responce

    @staticmethod
    def GetPorts()->List:
        ports = list_ports.comports()
        result = []
        for p in ports:
            result.append(  {"id": str(p.device), "manufacturer" : str(p.manufacturer), "description": str(p.description),"hwid": str(p.hwid) } )
        return result





class ISDPCmd:


    GROUPID = 3

    ETYPE_CMD   = 0
    ETYPE_RESP  = 1
    ETYPE_ASYNC = 2

    TYPESTR = ['C','R','A']

    def __init__(self,frame:ISDPFrame):
        self.frame = frame


    @property
    def ETYPEStr(self)->str:
        return  '%02X' % self.ETYPE if self.ETYPE>=len(ISDPCmd.TYPESTR) else ISDPCmd.TYPESTR[self.ETYPE]

    @staticmethod  
    def MakeGID(etype:int):
        return ((etype << 6) | (ISDPCmd.GROUPID))

    @property
    def Frame(self)->ISDPFrame:
        return self.frame

    @property
    def ETYPE(self)->int:
        return (self.GID & 0xC0) >> 6

    @property
    def GID(self)->int:
        return self.frame.GetByteAt(0)

    @property
    def GroupID(self)->int:
        return self.GID & 0x3F

    @property
    def EID(self)->int:
        return self.frame.GetByteAt(1)

    @property
    def Payload(self)->bytearray:
        return self.frame.GetBytesAfter(2)

    @staticmethod
    def FromFrame(frame:ISDPFrame)->ISDPCmd:
        cmd = ISDPCmd(frame)
        if ((cmd.ETYPE != ISDPCmd.ETYPE_RESP) and (cmd.ETYPE != ISDPCmd.ETYPE_ASYNC)):
            raise Exception("Wrong frame ETYPE in "+ frame.ToString())
        return cmd
       

    def ToString(self)->str:
        if (self.frame==None):
            return 'None'
        data = self.frame.ToBytes()
        if (len(data)==0):
            return 'Empty'
        return self.ETYPEStr +' ' + ('%02X ' % self.EID)  + ''.join( ('%02X' % x) for x in self.Payload)















class ISDP:


    SID_RESERVED                     = 0 
    SID_ACCELEROMETER                = 1 
    SID_MAGNETOMETER                 = 2 
    SID_ORIENTATION                  = 3 
    SID_GYROSCOPE                    = 4 
    SID_LIGHT                        = 5 
    SID_PRESSURE                     = 6 
    SID_TEMPERATURE                  = 7 
    SID_PROXIMITY                    = 8 
    SID_GRAVITY                      = 9 
    SID_LINEAR_ACCELERATION          = 10
    SID_ROTATION_VECTOR              = 11
    SID_HUMIDITY                     = 12
    SID_AMBIENT_TEMPERATURE          = 13
    SID_UNCAL_MAGNETOMETER           = 14
    SID_GAME_ROTATION_VECTOR         = 15
    SID_UNCAL_GYROSCOPE              = 16
    SID_SMD                          = 17
    SID_STEP_DETECTOR                = 18
    SID_STEP_COUNTER                 = 19
    SID_GEOMAG_ROTATION_VECTOR       = 20
    SID_HEART_RATE                   = 21
    SID_TILT_DETECTOR                = 22
    SID_WAKE_GESTURE                 = 23
    SID_GLANCE_GESTURE               = 24
    SID_PICK_UP_GESTURE              = 25
    SID_BAC                          = 26
    SID_PDR                          = 27
    SID_B2S                          = 28
    SID_3AXIS                        = 29
    SID_EIS                          = 30
    SID_OIS                          = 31
    SID_RAW_ACCELEROMETER            = 32
    SID_RAW_GYROSCOPE                = 33
    SID_RAW_MAGNETOMETER             = 34
    SID_RAW_TEMPERATURE              = 35
    SID_CUSTOM_PRESSURE              = 36
    SID_MIC                          = 37
    SID_TSIMU                        = 38
    SID_RAW_PPG                      = 39
    SID_HRV                          = 40
    SID_SLEEP_ANALYSIS               = 41
    SID_BAC_EXTENDED                 = 42
    SID_BAC_STATISTICS               = 43
    SID_FLOOR_CLIMB_COUNTER          = 44
    SID_ENERGY_EXPENDITURE           = 45
    SID_DISTANCE                     = 46
    SID_SHAKE                        = 47
    SID_DOUBLE_TAP                   = 48
    SID_TYPE_CUSTOM3                 = 52 # <--
    SID_TYPE_CUSTOM4                 = 53 # <--


    EID_WHO_AM_I                     = 0x10
    EID_RESET                        = 0x11
    EID_SETUP                        = 0x12
    EID_CLEANUP                      = 0x13
    EID_SELF_TEST                    = 0x15
    EID_GET_FW_INFO                  = 0x16
    EID_PING_SENSOR                  = 0x17
    EID_START_SENSOR                 = 0x19
    EID_STOP_SENSOR                  = 0x1A
    EID_SET_SENSOR_PERIOD            = 0x1B
    EID_SET_SENSOR_TIMEOUT           = 0x1C
    EID_FLUSH_SENSOR                 = 0x1D
    EID_SET_SENSOR_BIAS              = 0x1E
    EID_GET_SENSOR_BIAS              = 0x1F
    EID_SET_SENSOR_MMATRIX           = 0x20
    EID_GET_SENSOR_DATA              = 0x21
    EID_GET_SW_REG                   = 0x22
    EID_SET_SENSOR_CFG               = 0x23 # <--
    EID_GET_SENSOR_CFG               = 0x24
    EID_NEW_SENSOR_DATA              = 0x30

    


    CONFIG_RESERVED        = 0     # reserved - do not use 
    CONFIG_REFERENCE_FRAME = 1     # sensor reference frame (aka 'mouting matrix') 
    CONFIG_GAIN            = 2     # sensor gain to be applied on sensor data 
    CONFIG_OFFSET          = 3     # sensor offset to be applied on sensor data 
    CONFIG_CONTEXT         = 4     # arbitray context buffer 
    CONFIG_FSR             = 5     # sensor's full scale range 
    CONFIG_BW              = 6     # sensor's bandwidth 
    CONFIG_AVG             = 7     # sensor's average 
    CONFIG_RESET           = 8     # Reset the specified service 
    CONFIG_POWER_MODE      = 9     # Change the power mode of the sensor 
    CONFIG_CUSTOM          = 32    # base value to indicate custom config 
    CONFIG_MAX             = 64    # absolute maximum value for config type 


    INV_SENSOR_CONFIG_RESERVED                  = 0          
    INV_SENSOR_CONFIG_MOUNTING_MATRIX           = 1
    INV_SENSOR_CONFIG_GAIN                      = 2 
    INV_SENSOR_CONFIG_OFFSET                    = 3
    INV_SENSOR_CONFIG_CONTEXT                   = 4
    INV_SENSOR_CONFIG_FSR                       = 5
    INV_SENSOR_CONFIG_BW                        = 6
    INV_SENSOR_CONFIG_AVG                       = 7
    INV_SENSOR_CONFIG_RESET                     = 8
    INV_SENSOR_CONFIG_POWER_MODE                = 9
    INV_SENSOR_CONFIG_MIN_PERIOD                = 10
    INV_SENSOR_CONFIG_CUSTOM                    = 128     
    INV_SENSOR_CONFIG_PRED_GRV                  = 131     
    INV_SENSOR_CONFIG_BT_STATE                  = 132     
    INV_SENSOR_CONFIG_REPORT_EVENT              = 133 
    INV_SENSOR_CONFIG_FNM_OFFSET                = 134   
    INV_SENSOR_CONFIG_ALGO_SETTINGS             = 138  # <--
    INV_SENSOR_CONFIG_MAX                       = 255     


    INV_ERROR_SUCCESS      = 0,     # no error 
    INV_ERROR              = 0xFF   # unspecified error 
    INV_ERROR_NIMPL        = 0xFE   # function not implemented for given arguments 
    INV_ERROR_TRANSPORT    = 0xFD   # error occured at transport level 
    INV_ERROR_TIMEOUT      = 0xFC   # action did not complete in the expected `time window 
    INV_ERROR_SIZE         = 0xFB   # size/length of given arguments is not suitable to complete requested action 
    INV_ERROR_OS           = 0xFA   # error related to OS 
    INV_ERROR_IO           = 0xF9   # error related to IO operation 
    INV_ERROR_MEM          = 0xF7   # not enough memory to complete requested action 
    INV_ERROR_HW           = 0xF6   # error at HW level 
    INV_ERROR_BAD_ARG      = 0xF5   # provided arguments are not good to perform requestion action 
    INV_ERROR_UNEXPECTED   = 0xF4   # something unexpected happened 
    INV_ERROR_FILE         = 0xF3   # cannot access file or unexpected format 
    INV_ERROR_PATH         = 0xF2   # invalid file path 
    INV_ERROR_IMAGE_TYPE   = 0xF1   # error when image type is not managed 
    INV_ERROR_WATCHDOG     = 0xF0   # error when device doesn't respond  to ping 



    GIDCMD = ISDPCmd.MakeGID(ISDPCmd.ETYPE_CMD)


    CFG_PAYLOAD_SIZE = 64


    @staticmethod
    def WhoAmI()->ISDPCmd:
        return  ISDPCmd( ISDPFrame.FromPayload( bytearray( [ ISDP.GIDCMD ,ISDP.EID_WHO_AM_I] ) ) )



    # 55aa 0300 03  19  20               -- en 32
    @staticmethod
    def Start(sensorid:int)->ISDPCmd:
        return  ISDPCmd( ISDPFrame.FromPayload( bytearray( [ ISDP.GIDCMD ,ISDP.EID_START_SENSOR, sensorid ] ) ) )

    @staticmethod
    def Stop(sensorid:int)->ISDPCmd:
        return  ISDPCmd( ISDPFrame.FromPayload( bytearray( [ ISDP.GIDCMD ,ISDP.EID_STOP_SENSOR, sensorid ] ) ) )


    @staticmethod
    def SetConfig(sensorid:int,cfgtype:int,cfgdata:bytearray)->ISDPCmd:
        cfgsize = len(cfgdata)
        assert cfgsize<=ISDP.CFG_PAYLOAD_SIZE
        data = bytearray( [ ISDP.GIDCMD ,ISDP.EID_SET_SENSOR_CFG , sensorid, cfgtype ,cfgsize  ] )
        data += cfgdata
        if (cfgsize<ISDP.CFG_PAYLOAD_SIZE):
            data += bytearray(ISDP.CFG_PAYLOAD_SIZE - cfgsize)
        return  ISDPCmd( ISDPFrame.FromPayload( data ) )




    # 55AA 4500 03 23 20   05 04 02000000...      - setconfig 32 fsr=2
    @staticmethod
    def SetConfigFSR(sensorid:int,fsrvalue:int)->ISDPCmd:
        return ISDP.SetConfig(sensorid,ISDP.CONFIG_FSR,fsrvalue.to_bytes(4, 'little'))


    # 55aa 0700 03  1B  01 20 A1 07 00   -- odr acc 500
    @staticmethod
    def SetConfigODR(sensorid:int,odr_hz:int)->ISDPCmd:
        tomicriseconds = 1000000
        period_ms = int( (1/odr_hz)*tomicriseconds ) if (odr_hz!=0) else 0
        period_data = period_ms.to_bytes(4, 'little')
        data = bytearray( [ ISDP.GIDCMD ,ISDP.EID_SET_SENSOR_PERIOD , sensorid ] )
        data += period_data 
        return  ISDPCmd( ISDPFrame.FromPayload( data ) )



    # odr racc 1/100
    # 55aa 0700 03  1b 20 10 27 00 00
    # 55aa 0300 43  1b 00




    class Data:
        pass

    class CmdDecoder:

        def Bytes_to_uint16(data:bytearray,pos:int):
            assert len(data)>=pos+2
            return int.from_bytes(data[pos:pos+2],   byteorder='little', signed=False)

        def Bytes_to_uint24(data:bytearray,pos:int):
            assert len(data)>=pos+3
            return int.from_bytes(data[pos:pos+3],   byteorder='little', signed=False)

        def Bytes_to_int16(data:bytearray,pos:int):
            assert len(data)>=pos+2
            return int.from_bytes(data[pos:pos+2],   byteorder='little', signed=True)

        def Bytes_to_int32(data:bytearray,pos:int):
            assert len(data)>=pos+4
            return int.from_bytes(data[pos:pos+4],   byteorder='little', signed=True)


        def Bytes_to_int24(data:bytearray,pos:int):
            assert len(data)>=pos+3
            return int.from_bytes(data[pos:pos+3],   byteorder='little', signed=True)


        def Bytes_to_int16_Q11(data:bytearray,pos:int):
            return float( ISDP.CmdDecoder.Bytes_to_int16(data,pos) /  (1 << 11) )

        def Bytes_to_int16_Q4(data:bytearray,pos:int):
            return float( ISDP.CmdDecoder.Bytes_to_int16(data,pos) /  (1 << 4) )


        def Decode(self,c:ISDPCmd)->Data:
            return None


    class CmdSensorData(CmdDecoder):

        def Decode(self,c:ISDPCmd)->ISDP.Data:
            if (c.EID!=ISDP.EID_NEW_SENSOR_DATA):
                return None
            r = ISDP.Data()
            d = c.Payload
            if (len(d)<6):
                raise Exception("Wrong payload length in "+ c.ToString())
            r.sensorst = d[0] | 0x03
            r.sensorid = d[1]
            r.timestamp = int.from_bytes(d[2:6], byteorder='little', signed=False)
            return r


    class CmdSensorDataAccel(CmdSensorData):

        def Decode(self,c:ISDPCmd)->ISDP.Data:
            r = super().Decode(c)
            if (r==None):
                return None
            if (r.sensorid!=ISDP.SID_ACCELEROMETER):
                return None
            d = c.Payload
            if (len(d)!=13):
                raise Exception("Wrong sensor data length in "+ c.ToString())
            r.accuracy = d[12]
            r.x = ISDP.CmdDecoder.Bytes_to_int16_Q11(d,6) ;
            r.y = ISDP.CmdDecoder.Bytes_to_int16_Q11(d,8) ;
            r.z = ISDP.CmdDecoder.Bytes_to_int16_Q11(d,10) ;
            return r

    class CmdSensorDataGyro(CmdSensorData):

        def Decode(self,c:ISDPCmd)->ISDP.Data:
            r = super().Decode(c)
            if (r==None):
                return None
            if (r.sensorid!=ISDP.SID_GYROSCOPE):
                return None
            d = c.Payload
            if (len(d)!=13):
                raise Exception("Wrong sensor data length in "+ c.ToString())
            r.accuracy = d[12]
            r.x = ISDP.CmdDecoder.Bytes_to_int16_Q4(d,6) ;
            r.y = ISDP.CmdDecoder.Bytes_to_int16_Q4(d,8) ; 
            r.z = ISDP.CmdDecoder.Bytes_to_int16_Q4(d,10); 
            return r




    class CmdSensorDataRawAccGyro(CmdSensorData):

        # 55aa 0e00 83 30 00 20  3ec35b42  5400 80ff 5820  - EVENT D SENSOR_RAW_ACCELEROMETER id: 0x00000020 t: 1113310014 us: 0 data: 84 -128 8280
        def Decode(self,c:ISDPCmd)->ISDP.Data:
            r = super().Decode(c)
            if (r==None):
                return None
            if (r.sensorid!=ISDP.SID_RAW_ACCELEROMETER) and (r.sensorid!=ISDP.SID_RAW_GYROSCOPE):
                return None
            d = c.Payload
            if (len(d)==12):
                r.x = ISDP.CmdDecoder.Bytes_to_int16(d,6)
                r.y = ISDP.CmdDecoder.Bytes_to_int16(d,8);
                r.z = ISDP.CmdDecoder.Bytes_to_int16(d,10);
                return r
            if (len(d)==15):
                r.x = ISDP.CmdDecoder.Bytes_to_int24(d,6)
                r.y = ISDP.CmdDecoder.Bytes_to_int24(d,9);
                r.z = ISDP.CmdDecoder.Bytes_to_int24(d,12);
                return r

            raise Exception("Wrong sensor data length in "+ c.ToString())

            return r






    DECODERS = [CmdSensorDataAccel(),CmdSensorDataGyro(),CmdSensorDataRawAccGyro()]

    def Decode(c:ISDPCmd,customdecoders:List[CmdDecoder] = None)->Data:
        decoders = ISDP.DECODERS if (customdecoders==None) else customdecoders 
        for d in decoders:
            r = d.Decode(c)
            if (r!=None):
                return r
        raise Exception('No decoder found for '+ c.ToString())





   





if __name__ == "__main__":




    ports = ISDPTransport.GetPorts()
    for p in ports:
        print(p['id'])



    PORT = 'COM5'
    TIMEOUT_SEC = 1






    def Run(t:ISDPTransport,c:ISDPCmd)->ISDPCmd:
        print("> "+c.ToString())
        r = t.MakeATalk( c, TIMEOUT_SEC)
        if (r==None):
            print("< No responce" )
        else:
            print("< " + r.ToString() )
        return r

    def OnProtocolError(message):
        print(message)


    t = ISDPTransport(PORT,OnProtocolError)
    t.Open();


    Run( t, ISDP.WhoAmI() )

    Run( t, ISDP.SetConfigFSR(ISDP.SID_RAW_ACCELEROMETER,8) )
    #Run( t, ISDP.SetConfigODR(ISDP.SID_RAW_ACCELEROMETER,5) )
    #Run( t, ISDP.Start( ISDP.SID_RAW_ACCELEROMETER ) )
    #Run( t, ISDP.Start( ISDP.SID_ACCELEROMETER ) )
    #Run( t, ISDP.Start( ISDP.SID_GYROSCOPE  ) )



    n = 0
    while(n<100) and (t.IsConnected):
        if not t.WaitEvent(3):
            print("No responce")
            break;
        ee = t.GetAllEvents()
        for e in ee:
            #print("> " + e.ToString() )
            r = ISDP.Decode(e)
            print( "%02X   %010i   %-10.4f %-10.4f %-10.4f" % (r.sensorid,r.timestamp, r.x,r.y,r.z )   )
            #print( "%02X   %010i   %-10i %-10i %-10i" % (r.sensorid,r.timestamp, r.x,r.y,r.z )   )
        n+=1
    

    #Run( t, ISDP.Stop( ISDP.SID_GYROSCOPE  ) )
    #Run( t, ISDP.Stop( ISDP.SID_ACCELEROMETER ) )
    Run( t, ISDP.Stop( ISDP.SID_RAW_ACCELEROMETER ) )
    

    time.sleep(1)
    t.Close()



