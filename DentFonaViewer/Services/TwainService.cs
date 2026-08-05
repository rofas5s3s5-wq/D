using System;
using System.Threading.Tasks;
using NTwain;
using NTwain.Data;
using NTwain.Triple;
using System.Windows.Interop;

namespace DentFonaViewer.Services
{
    // Simple TWAIN service wrapper. This is a minimal implementation and may need
    // adaptation to your scanner's TWAIN driver and application requirements.
    public class TwainService : IDisposable
    {
        private TwainSession? _session;
        private DataSource? _ds;
        private readonly IntPtr _hwnd;

        public bool IsOpen { get; private set; }

        public TwainService(IntPtr? hwnd = null)
        {
            // hwnd is required for NTwain messages. If null, TWAIN may still work but some drivers require a window handle.
            _hwnd = hwnd ?? IntPtr.Zero;
        }

        public void Open()
        {
            if (IsOpen) return;
            var appId = new AppId("DentFona", "DentFona Viewer", "DentFona Viewer TWAIN", new Version(1, 0, 0, 0));
            _session = new TwainSession(appId);
            _session.TransferReady += (s, e) => { /* handle if needed */ };
            _session.DataTransferred += (s, e) => { /* handle if needed */ };
            _session.Open();
            IsOpen = true;
        }

        public void Close()
        {
            if (!IsOpen) return;
            _ds?.Close();
            _session?.Close();
            _session = null;
            _ds = null;
            IsOpen = false;
        }

        public async Task<byte[]?> CaptureAsync()
        {
            if (!IsOpen) Open();
            if (_session == null) return null;

            // Choose default datasource
            if (_ds == null)
            {
                _ds = _session.FirstOrDefault();
                if (_ds == null) throw new InvalidOperationException("No TWAIN data source (scanner) found.");
                _ds.Open();
            }

            // Use the data source to acquire image (this is simplified and driver-dependent)
            var tcs = new TaskCompletionSource<byte[]?>();

            _ds.DataTransferred += (s, e) =>
            {
                try
                {
                    if (e.NativeData?.Length > 0)
                    {
                        tcs.TrySetResult(e.Image?.ToArray());
                    }
                    else if (e.Image != null)
                    {
                        tcs.TrySetResult(e.Image.ToArray());
                    }
                    else tcs.TrySetResult(null);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            };

            _ds.Acquire();

            var result = await tcs.Task;
            return result;
        }

        public void Dispose()
        {
            Close();
        }
    }
}
