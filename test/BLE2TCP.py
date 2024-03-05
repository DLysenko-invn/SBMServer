#!/usr/bin/env python3

#  Copyright © 2020 InvenSense Inc. All rights reserved.
#
#
#  This software, related documentation and any modifications thereto (collectively “Software”) is subject
#  to InvenSense and its licensors' intellectual property rights under U.S. and international copyright
#  and other intellectual property rights laws.
#
#
#  InvenSense and its licensors retain all intellectual property and proprietary rights in and to the Software
#  and any use, reproduction, disclosure or distribution of the Software without an express license agreement
#  from InvenSense is strictly prohibited.



import socket
import time
import threading
import json





class PacketWaitObject:
    def __init__(self, optype,code,devindex,scindex):
        self.optype = optype
        self.code = code
        self.devindex = devindex
        self.scindex = scindex
        self.event = threading.Event()
        self.result = None

    def Check(self,pkt):
        if (pkt.optype != self.optype):
            return False
        if (pkt.code != self.code):
            return False
        if (self.devindex!=Packet.INDEX_NONE) and (pkt.devindex!=self.devindex):
            return False
        if (self.scindex!=Packet.INDEX_NONE) and (pkt.scindex!=self.scindex):
            return False
        
        self.result = pkt
        self.event.set()

        return True

    def Wait(self,timeout_sec):
        return self.event.wait(timeout_sec)

    def Cancel(self):
        self.event.set()




class BLEWatcherList:

    TEXT_DELIM = " "
    TEXT_SMARTBUG = "SmartBug"
    TEXT_BLE = "BLE"
    TEXT_SERIAL = "USB"
    TEXT_UNKNOWN = "Unknown"
    TEXT_DEVICE = "Device"
    TEXT_INDEX = "#"

    MARK_SMARTBUG = "smartbug"
    MARK_BLE = "BluetoothLE#BluetoothLE"
    MARK_SERIAL = "\\\\?\\"
    MARK_VIDPIDLIST = ["VID_1915&PID_520F"]
    MARK_COM = "com"

    TMP = 1

    def __init__(self):
        self.mutex = threading.Lock()
        self.devlist = []
        self.filterstr = None
        self.Clear()
        self.Finalaze()

    def IsRunning(self)->bool:
        return self.isstarted 

    def FindByRawIdUnsafe(self,rawid:str)->dict:
        for devinfo in self.devlist:
            if (devinfo["rawid"]==id):
                return devinfo
        return None

    def FindByShortIdUnsafe(self,shortid:str)->dict:
        for devinfo in self.devlist:
            if (devinfo["id"]==shortid):
                return devinfo
        return None


#Device found "COM4" - "\\?\BTHENUM#{00001101-0000-1000-8000-00805F9B34FB}_LOCALMFG&0000#7&ECA461D&0&000000000000_0000000A#{86e0d1e0-8089-11d0-9ce4-08003e301f73}"
#Device found "TDK - Smartbug2" - "BluetoothLE#BluetoothLE00:28:f8:93:14:84-6d:0f:ba:00:09:68"
#Device found "TDK - Smartbug2" - "BluetoothLE#BluetoothLE00:28:f8:93:14:84-6d:0f:ba:00:00:14"
#Device found "TDK - Smartbug2" - "BluetoothLE#BluetoothLE00:28:f8:93:14:84-6d:0f:ba:00:00:16"
#Device found "" - "BluetoothLE#BluetoothLE00:28:f8:93:14:84-71:3e:59:e0:cc:b6"
#Device found "COM3" - "\\?\PCI#VEN_8086&DEV_9D3D&SUBSYS_07A81028&REV_21#3&11583659&0&B3#{86e0d1e0-8089-11d0-9ce4-08003e301f73}"
#Device found "COM11" - "\\?\USB#VID_1915&PID_520F&MI_01#6&161E5CA0&0&0001#{86e0d1e0-8089-11d0-9ce4-08003e301f73}"
#Device found "" - "BluetoothLE#BluetoothLE00:28:f8:93:14:84-61:dc:1e:c7:ce:65"
#Device found "" - "BluetoothLE#BluetoothLE00:28:f8:93:14:84-f1:de:72:77:fb:09"
#Device found "" - "BluetoothLE#BluetoothLE00:28:f8:93:14:84-4c:12:dd:4a:25:98"
#Device found "" - "BluetoothLE#BluetoothLE00:28:f8:93:14:84-d0:0d:ee:e3:28:8f"


    def MakeShortIdUniqueUnsafe(self,shortid0:str)->str:
        index = 0
        while(True):
            shortid = shortid0 + ( "" if index==0 else (self.TEXT_DELIM + self.TEXT_INDEX + str(index)) )
            if (self.FindByShortIdUnsafe(shortid)==None ):
                break
            index+=1
            if (index>1000):
                raise Exception('Fatal Indexing Device Error')
            continue
        return shortid


    def MakeShortIdUnsafe(self,name:str,rawid:str)->str:
        devtype = self.TEXT_UNKNOWN
        devsuffix = ""
        if  rawid.lower().startswith( self.MARK_BLE.lower()) :
            devtype = self.TEXT_BLE
            if (len(rawid)>11):
                devsuffix = self.TEXT_DELIM + (rawid[-11:]).upper()
        if  rawid.lower().startswith( self.MARK_SERIAL.lower()) :
            devtype = self.TEXT_SERIAL
            devsuffix = self.TEXT_DELIM + name

        devname = self.TEXT_DEVICE
        if self.MARK_SMARTBUG.lower() in name.lower(): 
            devname = self.TEXT_SMARTBUG
        for vp in self.MARK_VIDPIDLIST:
            if vp.lower() in rawid.lower():
                devname = self.TEXT_SMARTBUG

        return self.MakeShortIdUniqueUnsafe(devname + self.TEXT_DELIM + devtype + devsuffix)


    def Add(self,name:str,id:str):
        if (self.isfinalized):
            return
        if (len(name.strip())==0):
            return

        self.mutex.acquire()
        isnew = self.FindByRawIdUnsafe(id)==None
        if (isnew):
            shortid = self.MakeShortIdUnsafe(name,id)
            self.devlist.append( {"name":name,"rawid":id, "id": shortid } )
            print("Device #%03i '%s'" % ( self.index,shortid ) )
            self.index+=1
        self.mutex.release()

    def Clear(self):
        self.mutex.acquire()
        self.devlist.clear()
        self.index=0
        self.isfinalized=False
        self.filterstr=None
        self.isstarted = False
        self.mutex.release()

    def SetFilter(self,filterstr:str):
        self.mutex.acquire()
        self.filterstr=filterstr
        self.mutex.release()

    def Initialize(self):
        self.mutex.acquire()
        self.devlist.clear()
        self.isfinalized=False
        self.filterstr=None
        self.isstarted = True
        self.mutex.release()



    def Finalaze(self):
        self.mutex.acquire()
        self.isfinalized=True
        self.isstarted = False
        self.mutex.release()


    def GetDevsListUnsafe(self)->dict:
        devs = []
        for d in self.devlist:
            if (self.filterstr!=None) and (not (self.filterstr in d["name"])):
                continue
            devs.append(d)
        return devs

    def Get(self)->dict:
        self.mutex.acquire()
        result =  {     "index":self.index,
                        "started":self.isstarted,
                        "finalized": self.isfinalized,
                        "filter":self.filterstr,
                        "devices": self.GetDevsListUnsafe() 
                  } 
        self.mutex.release()
        return result


    def FindByShortId(self,shortid:str)->dict:
        self.mutex.acquire()
        #todo: strip index if needed
        result = None
        for devinfo in self.devlist:
            if (devinfo["id"]==shortid):
                result = devinfo
                break
        self.mutex.release()
        return result


    def GetDeviceRawId(self,shortid:str)->str:
        d = self.FindByShortId(shortid)
        return d["rawid"] if (d!=None) else None

    def GetDeviceName(self,shortid:str)->str:
        d = self.FindByShortId(shortid)
        return d["name"] if (d!=None) else None




class BLE2TCP:

    PROTOCOL_TIMEOUT_SEC = 10
    DEVCONNECT_TIMEOUT_SEC = 20
    SUBSCRIBE_TIMEOUT_SEC = 20

    def __init__(self,host,port):

        self.connectevent = threading.Event()
        self.connectevent.clear()
        self.isconnected = False
        self.transport = TransportSocket(host,port)
        self.client = TCPClient(self.transport,self.ProcessPacket,self.OnTransportConnected)
        self.client.Start();

        self.wait_objects_list = list()
        self.wait_objects_mutex = threading.Lock()
        self.devices_list = dict() # dict[BLEDevice]
        self.watcherdata = BLEWatcherList()

    def IsConnected(self):
        return self.isconnected

    def Command(self,code):
        packet = Packet.MakeSimpleCommand(code)
        self.transport.WriteBytes( packet )


    def Close(self):
        self.client.Stop();
        self.client = None
        self.transport.Stop()
        self.transport = None;


    def WaitTillConnected(self,timeout_sec):
        return self.connectevent.wait(timeout_sec)

    def OnTransportConnected(self,isconnected):
        self.isconnected = isconnected

        if (not isconnected):
            self.connectevent.clear()
            for pwo in self.wait_objects_list:
                pwo.Cancel()
            self.wait_objects_mutex.acquire()
            self.wait_objects_list.clear()
            self.wait_objects_mutex.release()
            for dev in self.devices_list.values():
                dev.Disable()
            self.devices_list.clear()
        else:
            self.connectevent.set()




    def OnDeviceFound(self,name,id):
        #print( 'Device found "%s" - "%s"' %  (  name,id ) )
        self.watcherdata.Add(name,id)




        
    def Disconnect(self,devindex):

        if (not (devindex in self.devices_list)):
            print("Unknown device index #%i " % devindex)
            return

        bytes = Packet.MakeCommand(Packet.OP_disconnect,devindex,Packet.INDEX_NONE)

        pkt = self.Talk(bytes,Packet.OP_disconnect,devindex,Packet.INDEX_NONE)
        if (pkt==None):
            return False

        self.devices_list.pop(pkt.devindex,None)

        return True


    def AddDevice(self, devindex, devid):
        dev = BLEDevice(self,devid,devindex)
        self.devices_list[devindex] = dev


    
    def Connect(self,bledevid,callback_proc=None):

        bledevid_name = bledevid
        bledevid = self.watcherdata.GetDeviceRawId(bledevid)

        if (bledevid==None) or (len(bledevid)==0):
            print('Cannot find device ID corresponded the name "%s"' % ( 'NULL' if bledevid_name==None else str(bledevid_name) ) )
            return None

        print('Using BLE device ID "%s"' % bledevid )

        payload = {  "id": bledevid }
        bytes = Packet.MakeJsonCommand(Packet.OP_connect,Packet.INDEX_NONE,Packet.INDEX_NONE,payload)

        pkt = self.Talk(bytes,Packet.OP_connect,Packet.INDEX_NONE,Packet.INDEX_NONE)
        if (pkt==None) or (pkt.devindex == Packet.INDEX_NONE):
            return None

        rc = self.devices_list[pkt.devindex].WaitTillConnected(BLE2TCP.DEVCONNECT_TIMEOUT_SEC)

        if (callback_proc!=None):
            self.devices_list[pkt.devindex].SetCallback(callback_proc)
        
        if (not rc):
            return None

        return self.devices_list[pkt.devindex];


    def RunWatcher(self,isstart,filterstr=None):
        
        if (self.watcherdata.IsRunning() == isstart):
            return True

        if (isstart):
            op = Packet.OP_run_watcher
            self.watcherdata.Initialize()
            self.watcherdata.SetFilter(filterstr)
        else:
            op = Packet.OP_stop_watcher

        bytes = Packet.MakeSimpleCommand(op)
        pkt = self.Talk(bytes,op,Packet.INDEX_NONE,Packet.INDEX_NONE)
        if (pkt==None):
            return False

        return True

    def GetWatcherList(self):
        return self.watcherdata.Get()

    def GetDeviceName(self,bledevid:str)->str:
        return self.watcherdata.GetDeviceName(bledevid)



    def Talk(self,send_bytes,code,devindex,scindex):
        
        if (not self.IsConnected()):
            return None

        pwo = PacketWaitObject(Packet.TP_response,code,devindex,scindex)
        self.wait_objects_mutex.acquire()
        self.wait_objects_list.append( pwo )
        self.wait_objects_mutex.release()

        self.transport.WriteBytes(send_bytes)
        pwo.Wait(BLE2TCP.PROTOCOL_TIMEOUT_SEC)

        return pwo.result






    def GotResponse(self,pkt):
        if (pkt.iserror):
            return

        if (pkt.code == Packet.OP_connect):
            if  (pkt.devindex == Packet.INDEX_NONE):
                print("Protocol error: wrong device index")
            else:
                r = Packet.Decode(pkt.data)
                self.AddDevice(pkt.devindex,r["id"])
                if (int(r["code"]) == Packet.RC_AlreadyStarted):
                    self.devices_list[pkt.devindex].SetConnected(True)
            return


    def GotDisconnect(self,pkt):
        if (pkt.devindex in self.devices_list):
            if (self.devices_list[pkt.devindex].IsConnected()):
                self.devices_list[pkt.devindex].SetConnected(False)
                print( "Device #%i '%s' disconnected" % (self.devices_list[pkt.devindex].index,self.devices_list[pkt.devindex].uuid))
        


    def GotIndication(self,pkt):
        if (pkt.iserror):
            return

        if (pkt.code == Packet.OP_device_found):
            r = Packet.Decode(pkt.data)
            self.OnDeviceFound(r["name"], r["id"])
            return

        if (pkt.code == Packet.OP_watcher_stopped):
            print( "Watcher stopped")
            self.watcherdata.Finalaze()
            return

        if (pkt.code == Packet.OP_connected):
            if (pkt.devindex in self.devices_list):
                self.devices_list[pkt.devindex].SetConnected(True)
                print( "Device #%i '%s' connected " % (self.devices_list[pkt.devindex].index,self.devices_list[pkt.devindex].uuid))
            return

        if (pkt.code == Packet.OP_connection_lost):
            self.GotDisconnect(pkt)
            return

        if (pkt.code == Packet.OP_notification):
            if (pkt.devindex in self.devices_list):
                self.devices_list[pkt.devindex].GotNotification(pkt)
            return

         #res = ''.join(format(x, '02x') for x in data) 




    def ProcessPacket(self,data):
        
        if (len(data)==0):
            return

        pkt = Pkt(data)

        if (pkt.iserror):
            r = Packet.Decode(pkt.data)
            print("Server error: "+r["text"])
        else:
            if (pkt.code == Packet.OP_disconnect):
                self.GotDisconnect(pkt)
            if (pkt.optype==Packet.TP_response):
                self.GotResponse(pkt)
                if (pkt.code != Packet.OP_read) and (pkt.code != Packet.OP_write):
                    r = Packet.Decode(pkt.data)
                    if ("text" in r) and (r["text"]!=None) and (len(r["text"])!=0):
                        print("Server: "+r["text"])
            if (pkt.optype==Packet.TP_indication):
                self.GotIndication(pkt)

        self.wait_objects_mutex.acquire()
        for pwo in self.wait_objects_list:
            if(pwo.Check(pkt)):
                self.wait_objects_list.remove(pwo)
        self.wait_objects_mutex.release()
       


















       
class TransportSocket:
    def __init__(self, host, port):
        self.host = host
        self.port = port
        self.sock = None
        self.sendcounter = 0
        self.recvcounter = 0


    def Start(self):
        self.Stop()
        self.sendcounter = 0
        self.recvcounter = 0
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        return self.sock.connect_ex((self.host, self.port))

  
    def ReadBytes(self,size):
        if (self.sock==None):
            return None
        result =  self.sock.recv(size)
        if (result!=None):
            n = len(result)
            self.recvcounter += n
            if (n != size):
                print("Read socket error %i %i" % (n , size))
        return result

    def Stop(self):
        if (self.sock==None):
            return
        s = self.sock
        self.sock = None
        try:
            s.shutdown(socket.SHUT_RDWR)
        except:
            pass
        try:
            s.close()
        except:
            pass


    def WriteBytes(self,data):
        if (self.sock==None):
            return 0
        self.sock.sendall(data)
        self.sendcounter += len(data)
        #print(">>> W: "+Packet.MakeString(data))
        return len(data)

    def Description(self):
        return str(self.host)+":"+str(self.port)



class TCPClient:

    RECONNECT_SEC = 10
    TIME_TO_CLOSE_SOCKET = 5

    def __init__(self, transport, packetparser_proc, connectevent_proc ):
        self.transport = transport
        self.packetparser_proc = packetparser_proc
        self.connectevent_proc = connectevent_proc
        self.startevent = threading.Event()
        self.stopevent = threading.Event()
        self.cancelreconnect = threading.Event()

    def Start(self):
        self.isstopthread = False
        self.startevent.clear()
        self.stopevent.clear()
        self.cancelreconnect.clear()
        self.thread = threading.Thread(target = self.DoWork)
        self.thread.start()
        self.startevent.wait()



    def DoWork(self):

        self.startevent.set()

        while (not self.isstopthread):

            print("Connecting to "+self.transport.Description()+" ...")
            rc = self.transport.Start()
            if (rc != 0):
                print("Connect error. Retry in %i seconds" % ( self.RECONNECT_SEC) )
                self.cancelreconnect.wait(self.RECONNECT_SEC)
                continue

            print("Socket connected")
            self.connectevent_proc(True)
            while (not self.isstopthread):

                try:
                    data1 = self.transport.ReadBytes(2);
                except Exception as e: 
                    #print("Socket read error "+str(e))
                    break

                if len(data1)!=2:
                    break
                size = int.from_bytes(data1, byteorder='little', signed=False)
                data2 = self.transport.ReadBytes(size);
                if len(data2)!=size:
                    break
                #print(">>> R: "+Packet.MakeString(data1+data2))
                self.packetparser_proc(data2)
     
            print("Disconnected")
            self.connectevent_proc(False)
            self.transport.Stop()

        self.stopevent.set()


    def Stop(self):
        self.isstopthread = True
        self.transport.Stop()
        self.cancelreconnect.set()
        self.stopevent.wait(self.TIME_TO_CLOSE_SOCKET)



class Pkt:
    def __init__(self,bytedata):


        data = bytearray(bytedata)

        opcode = data[0]
        (self.iserror,self.optype,self.code) = Packet.Split(opcode);
        self.devindex = data[1]
        self.scindex = data[2]
        
        self.data = data[3:]






class Packet:
    def __init__(self):
        pass

    INDEX_NONE=0

    TP_none = 0
    TP_indication = 1
    TP_command = 2
    TP_response =3

    OP_nop	                   = 0   
    OP_information	           = 1   
    OP_set_param	           = 2   
    OP_getparam	               = 3   
    OP_run_watcher	           = 4   
    OP_stop_watcher            = 5   
    OP_device_found	           = 6   
    OP_watcher_stopped	       = 7   
    OP_connect	               = 8   
    OP_disconnect	           = 9   
    OP_connected	           = 10  
    OP_connection_lost	       = 11  
    OP_result	               = 12  
    OP_serv_char_found	       = 13  
    OP_subscribe	           = 15  
    OP_unsubscribe	           = 16  
    OP_read	                   = 17  
    OP_write	               = 18  
    OP_notification	           = 19  
    OP_pair      	           = 20  
    OP_unpair      	           = 21  
    OP_get_sc                  = 22


    RC_Ok                      = 0 
    RC_AlreadyStarted          = 1 
    RC_Error                   = 100
    RC_ErrorFormat             = 101
    RC_ErrorNotFound           = 102
    RC_ErrorBLE                = 103



    @staticmethod
    def MakeString(data):
        size = data[0] + data[1]*0x100
        (iserror,optype,code) = Packet.Split(data[2])
        devindex = data[3]
        scindex = data[4]

        if (iserror):
            prefix="!"
        else:
            prefix=" "

        if   (optype==Packet.TP_indication):
            optypestr = "I"
        elif (optype==Packet.TP_command):
            optypestr = "C"
        elif (optype==Packet.TP_response):
            optypestr = "R"
        else:
            optypestr = "?"

        if (not iserror) and ((code == Packet.OP_read) or (code == Packet.OP_write) or (code == Packet.OP_notification)):
            payloadstr = ''.join(format(x, '02x') for x in data[5:]) 
        else:
            payloadstr = data[5:].decode("ascii")

        return "%s %s %02i %02X %02X %s" % (prefix,optypestr,code,devindex,scindex,payloadstr)
    

    @staticmethod
    def OpCode(iserror,optype,code):
        result=0
        if (iserror):
            result = result | 0x80
        result = result | (optype << 5);
        result = result | code;
        return (result & 0xFF)

    @staticmethod
    def Split(opcode):
        iserror = ((opcode & 0x80)!=0)
        type = (opcode & 0x60) >> 5
        code = opcode & 0x1F;
        return (iserror,type,code)

    @staticmethod
    def MakeSimpleCommand(code):
       return Packet.MakeBytes(False,Packet.TP_command,code,Packet.INDEX_NONE,Packet.INDEX_NONE,None)

    @staticmethod
    def MakeCommand(code,devindex,scindex):
       return Packet.MakeBytes(False,Packet.TP_command,code,devindex,scindex,None)

    @staticmethod
    def MakeCommandEx(code,devindex,scindex,payloadbytes):
       return Packet.MakeBytes(False,Packet.TP_command,code,devindex,scindex,payloadbytes)


    @staticmethod
    def MakeBytes(iserror,optype,code,devindex,scindex,payloadbytes):
        code = Packet.OpCode(iserror,optype,code)
        if(payloadbytes==None):
            n=0
        else:
            n = len(payloadbytes)
        n+=3;
        result = bytearray(n+2)
        result[0]  = n & 0xFF
        result[1]  = (n & 0xFF00) >> 8
        result[2]  = code
        result[3]  = devindex
        result[4]  = scindex
        if(payloadbytes!=None):
            result[5:] = payloadbytes
        return result


    @staticmethod
    def Decode(bytes):
        str = bytes.decode("ascii")
        obj = json.loads(str)
        return obj

    @staticmethod
    def Encode(obj):
        str = json.dumps(obj)
        bytes = str.encode("ascii")
        return bytes


    @staticmethod
    def MakeJson(iserror,optype,code,devindex,scindex,payload_obj):
        return Packet.MakeBytes(iserror,optype,code,devindex,scindex, Packet.Encode(payload_obj))
    
    @staticmethod
    def MakeJsonCommand(code,devindex,scindex,payload_obj):
        return  Packet.MakeBytes(False,Packet.TP_command,code,devindex,scindex, Packet.Encode(payload_obj))






class BLESubscription:
    def __init__(self,core,device,servid,charid,index,proc):
        self.core = core
        self.device = device
        self.index = index
        self.subscribed = False
        self.servid = servid
        self.charid = charid
        self.proc = proc
        self.issubscribed = False






class BLEDevice:
    def __init__(self,core,uuid,index):
        self.core = core
        self.index = index
        self.connectevent = threading.Event()
        self.isconnected = False
        self.uuid = uuid
        self.subscriptions_list = dict() 
        self.isdisabled = False
        self.callback = None

    def SetCallback(self,callback_proc):
        self.callback = callback_proc

    def SetConnected(self,isconnected):
        if (isconnected):
            self.isconnected = True
            self.connectevent.set()
        else:
            self.isconnected = False
            self.connectevent.clear()

        if (self.callback!=None):
            self.callback(self)


    def IsConnected(self):
        if (self.isdisabled):
            return False
        return self.isconnected;


    def WaitTillConnected(self,timeout):
        self.connectevent.wait(timeout)
        return self.isconnected

    def GetId(self):
        return self.uuid

    def GetIndex(self):
        return self.index

    def GetSCIndex(self,servid,charid):

        #todo: cache

        payload = {  "service": servid,  "characteristic": charid}
        bytes = Packet.MakeJsonCommand(Packet.OP_get_sc,self.index,Packet.INDEX_NONE,payload)

        pkt = self.core.Talk(bytes,Packet.OP_get_sc,self.index,Packet.INDEX_NONE)

        if (pkt==None):
            return Packet.INDEX_NONE

        return pkt.scindex



    def Read(self,servid,charid):
        if (self.isdisabled):
            return None

        scindex = self.GetSCIndex(servid,charid)

        if (scindex==Packet.INDEX_NONE):
            return None

        bytes = Packet.MakeCommand(Packet.OP_read,self.index,scindex)
        pkt = self.core.Talk(bytes,Packet.OP_read,self.index,scindex)

        if (pkt==None):
            return None

        return pkt.data
    

    def Write(self,servid,charid,bytedata):
        if (self.isdisabled):
            return False

        scindex = self.GetSCIndex(servid,charid)

        if (scindex==Packet.INDEX_NONE):
            return False

        bytes = Packet.MakeCommandEx(Packet.OP_write,self.index,scindex,bytedata)
        pkt = self.core.Talk(bytes,Packet.OP_write,self.index,scindex)

        if (pkt==None):
            return False

        return True


    def Subscribe(self,servid,charid,proc):
        if (self.isdisabled):
            return Packet.INDEX_NONE

        scindex = self.GetSCIndex(servid,charid)

        if (scindex==Packet.INDEX_NONE):
            return Packet.INDEX_NONE

        self.subscriptions_list[scindex] = BLESubscription(self.core,self,servid,charid,scindex,proc)

        bytes = Packet.MakeCommand(Packet.OP_subscribe,self.index,scindex)
        pkt = self.core.Talk(bytes,Packet.OP_subscribe,self.index,scindex)

        if (pkt==None):
            return Packet.INDEX_NONE

        return  scindex



    def Unsubscribe(self,scindex):
        if (self.isdisabled):
            return

        if (not (scindex in self.subscriptions_list)):
            print("BLEDevice.Unsubscribe: Unknown SC index (%i)" % scindex)
            return
       
        bytes = Packet.MakeCommand(Packet.OP_unsubscribe,self.index,scindex)
        pkt = self.core.Talk(bytes,Packet.OP_unsubscribe,self.index,scindex)

        if (pkt==None):
            return 

        self.subscriptions_list.pop(scindex, None)



    def GotNotification(self,pkt):
        if (pkt.scindex in self.subscriptions_list) and (self.subscriptions_list[pkt.scindex].proc!=None):
            self.subscriptions_list[pkt.scindex].proc(self.index,pkt.scindex,pkt.data)

    def Close(self):
        self.core.Disconnect(self.index)
        self.Disable()

    def Disable(self):
        self.isdisabled = True
        self.connectevent.set()
