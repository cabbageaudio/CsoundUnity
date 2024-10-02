﻿using System.Collections;
using UnityEngine;
using ASU = Csound.Unity.Utilities.AudioSamplesUtils;

namespace Csound.Unity.Utilities.MonoBehaviours
{
    /// <summary>
    /// Loads audio samples from an AudioClip into a Csound table
    /// </summary>
    public class TableLoader : MonoBehaviour
    {
        [Tooltip("The AudioClip from which the samples will be loaded")]
        [SerializeField] AudioClip _source;

        [Tooltip("The Csound table number that will be created and filled with the loaded samples")]
        [SerializeField] int _tableNumber = 100;

        [Tooltip("If true it will only get the left channel of the source AudioClip, otherwise it will read all channels")]
        [SerializeField] bool _forceToMono = true;

        [Tooltip("The channel that will be read when forceToMono is true")]
        [SerializeField] int _channel = 0;

        [Tooltip("If true, when more than one channel is present in the source AudioClip, it will create a table for each channel, " +
            "starting from tableNumber and increasing by one for each channel.\n" +
            "If false it will create an interleaved table with all the channels together. The number of channels will be written in the first index of the table")]
        [SerializeField] bool _splitChannels = false;

        [Tooltip("The starting point in seconds where to start to read for samples")]
        [SerializeField] float _startPoint = 0;

        [Tooltip("The end point in seconds where to start to read for samples. If zero it will read from startPoint to the end of the file")]
        [SerializeField] float _endPoint = 0;

        [Tooltip("If true, it will start loading tables on Start. Set it to false if you want to specify the source at runtime and create the tables after")]
        [SerializeField] bool _autoLoad = true;

        [Tooltip("Specify the instance of Csound where the table will be created. " +
            "If empty, the CsoundUnity component will be searched in this GameObject")]
        [SerializeField] CsoundUnity _csound;

        IEnumerator Start()
        {
            if (_autoLoad)
            {
                yield return Loading(_source);
            }
        }

        public void Load(AudioClip audioClip)
        {
            StartCoroutine(Loading(audioClip));
        }

        IEnumerator Loading(AudioClip audioClip)
        {
            if (!_csound)
            {
                _csound = GetComponent<CsoundUnity>();
                if (!_csound)
                {
                    Debug.LogWarning($"Csound.Unity.Utilities.LoadFiles.TableLoader: Csound not found in GameObject {this.name}");
                    yield break;
                }
            }

            while (!_csound.IsInitialized)
            {
                yield return null; //waiting for initialization
            }

            if (_forceToMono) // only reading one channel from the source AudioClip
            {
                var selectedSamples = GetSamples(true, audioClip, _channel, _startPoint, _endPoint);
                if (selectedSamples.Length == 0)
                {
                    Debug.LogError($"Csound.Unity.Utilities.LoadFiles.TableLoader: Cannot create table{_tableNumber} from audioClip {audioClip.name}, selection range is 0 samples!");
                    yield break;
                }
                Debug.Log($"Csound.Unity.Utilities.LoadFiles.TableLoader: samples loaded: {selectedSamples.Length}, creating table #{_tableNumber} from audioClip {audioClip.name}");
                var resMono = _csound.CreateTable(_tableNumber, selectedSamples);
                if (resMono == 0) Debug.Log($"Csound.Unity.Utilities.LoadFiles.TableLoader: Created table {_tableNumber} from audioClip {audioClip.name}!");
                else Debug.LogError($"Csound.Unity.Utilities.LoadFiles.TableLoader: Cannot create table {_tableNumber} from audioClip {audioClip.name}");
            }
            else
            {
                if (_splitChannels) // creating a table for each channel found in the source AudioClip
                {
                    for (var i = 0; i < audioClip.channels; i++)
                    {
                        var selectedSamples = GetSamples(true, audioClip, i, _startPoint, _endPoint);
                        if (selectedSamples.Length == 0)
                        {
                            Debug.LogError($"Csound.Unity.Utilities.LoadFiles.TableLoader: Cannot create table {_tableNumber} from audioClip {audioClip.name}, selection range is 0 samples!");
                            yield break;
                        }
                        var tableNumber = _tableNumber + i;
                        Debug.Log($"Csound.Unity.Utilities.LoadFiles.TableLoader: samples loaded: {selectedSamples.Length}, creating table #{tableNumber} from audioClip {audioClip.name}");
                        var resChannel = _csound.CreateTable(tableNumber, selectedSamples);
                        if (resChannel == 0) Debug.Log($"Csound.Unity.Utilities.LoadFiles.TableLoader: Created table {tableNumber} from audioClip {audioClip.name}!");
                        else Debug.LogError($"Csound.Unity.Utilities.LoadFiles.TableLoader: Cannot create table {tableNumber} from audioClip {audioClip.name}");
                    }
                }
                else // creating interleaved table
                {
                    var selectedSamples = GetSamples(false, audioClip, 0, _startPoint, _endPoint);
                    Debug.Log($"Csound.Unity.Utilities.LoadFiles.TableLoader: samples loaded: {selectedSamples.Length}, creating table #{_tableNumber} from audioClip {audioClip.name}");
                    var resInterleaved = _csound.CreateTable(_tableNumber, selectedSamples);
                    if (resInterleaved == 0) Debug.Log($"Csound.Unity.Utilities.LoadFiles.TableLoader: Created table {_tableNumber} from audioClip {audioClip.name}!");
                    else Debug.LogError($"Csound.Unity.Utilities.LoadFiles.TableLoader: Cannot create table {_tableNumber} from audioClip {audioClip.name}");
                }
            }
        }

        private double[] GetSamples(bool isMono, AudioClip audioClip, int channel, float startPoint, float endPoint)
        {
            double[] selectedSamples;
            int start;
            int end;

            if (isMono)
            {
                start = Mathf.CeilToInt(startPoint * audioClip.frequency);
                end = Mathf.CeilToInt(endPoint * audioClip.frequency);
                if (start < 0) start = 0;
                if (end <= 0 || end >= audioClip.samples) end = audioClip.samples;
                if (start > end || start >= audioClip.samples)
                {
                    Debug.LogWarning($"Csound.Unity.Utilities.LoadFiles.TableLoader: start point is higher than end point, it will be set to the beginning of the file");
                    start = 0;
                }
                var channelSamples = ASU.GetMonoSamples(audioClip, channel);
                selectedSamples = new double[channelSamples.Length];
                for (var i = 0; i < (end - start); i++)
                {
                    selectedSamples[i] = channelSamples[i + start];
                }
            }
            else
            {
                var interleavedSamples = ASU.GetSamples(audioClip);
                // AudioSampleUtils.GetSamples returns an interleaved table where the first index is the number of channels
                // that's why we have to add 1 here
                start = Mathf.CeilToInt(_startPoint * audioClip.frequency * audioClip.channels + 1);
                end = Mathf.CeilToInt(_endPoint * audioClip.frequency * audioClip.channels + 1);
                if (start < 0) start = 1;
                if (end <= 1 || end >= audioClip.samples) end = audioClip.samples + 1;
                if (start > end || start >= audioClip.samples)
                {
                    Debug.LogWarning($"Csound.Unity.Utilities.LoadFiles.TableLoader: start point is higher than end point, it will be set to the beginning of the file");
                    start = 0;
                }
                selectedSamples = new double[end - start];
                for (int i = 0, j = 0; i < (end - start); i++, j += audioClip.channels)
                {
                    //Debug.Log($"i: {i}, j: {j}, channels: {audioClip.channels}, start: {start}, end: {end}, start + j: {start + j}, length: {end - start}, selectedSamples.length: {selectedSamples.Length}, interleavedSamples.Length: {interleavedSamples.Length}");
                    selectedSamples[i] = interleavedSamples[start + j];
                }
            }
            if (selectedSamples.Length == 0)
            {
                Debug.LogError($"Csound.Unity.Utilities.LoadFiles.TableLoader: Cannot create table {_tableNumber} from audioClip {audioClip.name}, selection range is 0 samples!");
            }
            return selectedSamples;
        }
    }
}
