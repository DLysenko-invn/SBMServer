using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;




namespace DynamicProtocol
{

    class SIFData
    {

        public class ConfigFile
        {
            public class ConfigFile_common
            {
                public int    FS { get;set;}
                public double WIND_SIZE_SEC {get;set;}
                public int    AXIS_NUM {get;set;}
            }

            public class ConfigFile_sensor_Spectral
            {
                public int FFT_FEAS_ENABLE {get;set;}
            }

            public class ConfigFile_sensor_Time
            {
                public int TIME_FEAS_ENABLE {get;set;}
                public int HYST_THR_Q16 {get;set;}
                public int MIN_PEAK_DISTANCE {get;set;}
                public int MIN_PEAK_HEIGHT_Q16 {get;set;}
                public int T_CONFIG_NUM {get;set;}
                public int T_FILTERS_NUM {get;set;}
                public int T_NUM_FEAS_TOTAL {get;set;}
                public int[][] T_FiltBnA_Q28  {get;set;}
                public int[][] Temporal_Feas_Config {get;set;}
            }


            public class ConfigFile_sensor
            {
                public int? FSR { get;set;}
                public int FSR_LOG2 { get;set;}
                public int AXIS_NUM { get;set;}
                public ConfigFile_sensor_Spectral Spectral { get;set;}
                public ConfigFile_sensor_Time Time { get;set;}
            }

            public ConfigFile_common common {get;set;}
            public ConfigFile_sensor[] sensor {get;set;}

        }


        public class DTableFile
        {
            public int NODE_SIZE {get;set;}
            public int NUM_FEATURE_IDS {get;set;}
            public int[] decisionTreeThresholds {get;set;}
            public int[] decisionTreeFeatureIDs {get;set;}
            public int[] decisionTreeNextNodeRight {get;set;}
            public int[] decisionTreeThresholdsShift {get;set;}

        }



        public ConfigFile cfg;
        public DTableFile tab;

        public void Load(string configfilename,string dtabfilename)
        {   this.cfg = JsonSerializer.Deserialize<ConfigFile>( File.ReadAllText(configfilename) );
            this.tab = JsonSerializer.Deserialize<DTableFile>( File.ReadAllText(dtabfilename) );
        }



    }


    class ISDP_Newport
    {
        const double TIMEOUT_SEC = 0.5;
        const int DEFAULT_FSR = 8;


        public static ISDPCmd[] MakeConfig(string config_filename, string dtable_filename)
        { 
            SIFData d = new SIFData();
            d.Load(config_filename,  dtable_filename);
            List<ISDPCmd>  r = new List<ISDPCmd>( CfgToCmdList(d) );
            return r.ToArray();
        }



        static ISDPCmd  SetConfigALGO(int index, int[] data)
        { 
            const int CFGLEN = 14;

            int sensorid = ISDP.SID_TYPE_CUSTOM3;
            int cfgtype = ISDP.INV_SENSOR_CONFIG_ALGO_SETTINGS;
            int zero = 0;
            int n = data.Length;
            int size = n*4+4;
            byte[] cfgdata = new byte[]{};
            ISDP.ConcatByteArray(cfgdata, ISDP.UInt32ToBytes(size));
            ISDP.ConcatByteArray(cfgdata, ISDP.UInt32ToBytes(index));
            Debug.Assert(n<CFGLEN);

            for(int i=0;i<CFGLEN;i++)
                ISDP.ConcatByteArray(cfgdata, ISDP.Int32ToBytes( (i>=CFGLEN) ?  zero : data[i]));

            return ISDP.SetConfig(sensorid,cfgtype,cfgdata);
        }

        static IEnumerable<ISDPCmd> CfgToCmdList(SIFData d)
        {

            int acc_sensor_index = 0;
            double  WIND_SIZE_SAMPLE =  d.cfg.common.WIND_SIZE_SEC*d.cfg.common.FS ;
            int INV_DATA_WIND = (int)( (1<<30)/(WIND_SIZE_SAMPLE) ) ;
            int ACC_T_FILTERS_NUM = d.cfg.sensor[acc_sensor_index].Time.T_FILTERS_NUM;
            int ACC_T_CONFIG_NUM = d.cfg.sensor[acc_sensor_index].Time.T_CONFIG_NUM;
            int fsr;

            if (d.cfg.sensor[acc_sensor_index].FSR!=null)
            {   fsr = (int)d.cfg.sensor[acc_sensor_index].FSR;
            } else
            {   fsr = DEFAULT_FSR;
            }

            yield return ISDP.SetConfigFSR(ISDP.SID_RAW_ACCELEROMETER,fsr) ;
            yield return ISDP.SetConfigODR(ISDP.SID_RAW_ACCELEROMETER,d.cfg.common.FS);
            yield return ISDP.SetConfigODR(ISDP.SID_TYPE_CUSTOM3,d.cfg.common.FS);

            int[] cfgdata = new int[]{  d.cfg.common.FS,                                          // FS, 
                                        (int)WIND_SIZE_SAMPLE ,                                   // WIND_SIZE_SAMPLE, 
                                        INV_DATA_WIND,                                            // INV_DATA_WIND, 
                                        d.cfg.sensor[acc_sensor_index].Time.HYST_THR_Q16 ,        // ACC_HYST_THR_Q16, 
                                        d.cfg.sensor[acc_sensor_index].Time.MIN_PEAK_DISTANCE,    // ACC_MIN_PEAK_DISTANCE, 
                                        d.cfg.sensor[acc_sensor_index].Time.MIN_PEAK_HEIGHT_Q16,  // ACC_MIN_PEAK_HEIGHT_Q16, 
                                        ACC_T_CONFIG_NUM,                                         // ACC_T_CONFIG_NUM, 
                                        ACC_T_FILTERS_NUM,                                        // ACC_T_FILTERS_NUM, 
                                        d.cfg.sensor[acc_sensor_index].Time.T_NUM_FEAS_TOTAL,     // ACC_T_NUM_FEAS_TOTAL, 
                                        d.tab.NODE_SIZE,                                          // NODE_SIZE, 
                                        d.tab.NUM_FEATURE_IDS,                                    // NUM_FEATURE_IDS
                                         };

            yield return SetConfigALGO( 0,cfgdata);

            for(int i=0;i<ACC_T_FILTERS_NUM;i++)
            {   int index = i+1;
                int[] filtercoef = d.cfg.sensor[acc_sensor_index].Time.T_FiltBnA_Q28[i];
                yield return SetConfigALGO(   index, filtercoef );   //ACC_T_FiltBnA_Q28
            }

            for(int i=0;i<ACC_T_CONFIG_NUM;i++)
            {   int index = i+ACC_T_FILTERS_NUM+1;
                int[] feasconfig = d.cfg.sensor[acc_sensor_index].Time.Temporal_Feas_Config[i];
                yield return SetConfigALGO(   index, feasconfig ) ;//ACC_Temporal_Feas_Config
            }


            int dtable_node_size_packet_cnt;
            { 
                int[] dtT = d.tab.decisionTreeThresholds;
                int[] dtF = d.tab.decisionTreeFeatureIDs;
                int[] dtN = d.tab.decisionTreeNextNodeRight;
                int n = dtT.Length;
                int step = 4;
                Debug.Assert( (n==dtF.Length) && (n==dtN.Length) );
                dtable_node_size_packet_cnt = (int)(n/step) + ( (n % step==0) ? 0 : 1) ;
                int pos=0;
                for(int i=0;i<dtable_node_size_packet_cnt;i++)
                { 
                    int index = i+ACC_T_FILTERS_NUM+ACC_T_CONFIG_NUM+1;
                    int[] dt = new int[]{};

                    int j = (pos+step>n) ? (n - pos) : step;
                    Debug.Assert(j!=0);
                    ISDP.ConcatIntArray(dt,ISDP.SubIntArray(dtT,pos,j));
                    ISDP.ConcatIntArray(dt,ISDP.SubIntArray(dtF,pos,j));
                    ISDP.ConcatIntArray(dt,ISDP.SubIntArray(dtN,pos,j));
                    pos+=j;
                    yield return SetConfigALGO(   index, dt  );// decisionTreeThresholds, decisionTreeFeatureIDs, decisionTreeNextNodeRight

                }
            }

            int dtable_num_feature_ids_packet_cnt;
            { 
                int[] dtS = d.tab.decisionTreeThresholdsShift;
                int n = dtS.Length;
                int step = 14;
                dtable_num_feature_ids_packet_cnt = (int)(n/step) + ((n % step==0) ? 0 : 1) ;
                int pos=0;
                for(int i=0;i<dtable_num_feature_ids_packet_cnt;i++)
                { 
                    int index = i+ACC_T_FILTERS_NUM+ACC_T_CONFIG_NUM+1+dtable_node_size_packet_cnt;
                    int j = (pos+step>n) ? (n - pos) : step;
                    Debug.Assert(j!=0);
                    int[] dt =  ISDP.SubIntArray( dtS, pos, j);
                    pos+=j;
                    yield return SetConfigALGO(   index, dt  ) ;// decisionTreeThresholdsShift
                }
            }


        }


        public class CmdSensorCust3Label:ISDP.CmdSensorData
        { 
            // EVENT D SENSOR_CUSTOM3 id: 0x00000034 t: 1113310014 us: 0 data: 4.000000
            public override ISDP.Data Decode(ISDPCmd c)
            { 
                const int PAYLOADSIZE = 71;
                ISDP.Data r = base.Decode(c);

                if (r==null)
                    return null;
                if (r.sensorid!=ISDP.SID_TYPE_CUSTOM3)
                    return null;
                byte[] d = c.Payload;
                if (d.Length!=PAYLOADSIZE)
                    throw new ISDPException("Wrong data length in "+ c.ToString());
                int n = 6;
                int validsize = d[n];
                if (validsize!=1)
                    throw new ISDPException("Wrong valid data size in "+ c.ToString());
                n+=1;
                r.label = d[n];
                return r;
            }
        }



        public class CmdSensorCust4RawAcc:ISDP.CmdSensorData
        { 
            // [W] acc data = -1480 1480 63296.
            // [D] payload len = 73
            // 83,30,00,35,31,6c,5c,06,0c,38,fa,ff,ff,c8,05,00,00,40,f7,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00

            public override ISDP.Data Decode(ISDPCmd c)
            {
                const int PAYLOADSIZE = 71;
                ISDP.Data r = base.Decode(c);
                if (r==null)
                    return null;
                if (r.sensorid!=ISDP.SID_TYPE_CUSTOM4)
                    return null;
                byte[] d = c.Payload;
                if (d.Length!=PAYLOADSIZE)
                    throw new ISDPException("Wrong data length in "+ c.ToString());

                int n = 6;
                int validsize = d[n];
                if (validsize!=12)
                    throw new ISDPException("Wrong valid data size in "+ c.ToString());

                n+=1;
                r.ix =Bytes_to_int32(d,n);
                n+=4;
                r.iy = Bytes_to_int32(d,n);
                n+=4;
                r.iz = Bytes_to_int32(d,n);

                return r;
            }

        }




    }
}
