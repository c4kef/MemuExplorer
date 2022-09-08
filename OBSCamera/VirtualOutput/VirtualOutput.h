#pragma once

#include "../NativeOBS/video_output.h"
#include "Utils.h"
using namespace System;
using namespace System::Drawing;
using namespace System::Runtime;

#pragma comment(lib,"Advapi32.lib")
namespace VirtualCameraOutput {
	public ref class VirtualOutput
	{
		private:
			int width_;
			int height_;
			int fps_;
			FourCC fourcc_;

		protected:
			core::video_output* vo_;

		public:
			VirtualOutput(int width, int height, int fps, FourCC fourcc) {
				// https://github.com/obsproject/obs-studio/blob/9da6fc67/.github/workflows/main.yml#L484
				/*//Компьютер\HKEY_LOCAL_MACHINE\SOFTWARE\Classes\CLSID\{A3FCE0F5-3493-419F-958A-ABA1250EC20B}
				//Компьютер\HKEY_LOCAL_MACHINE\SOFTWARE\Classes\CLSID\{40E27B1D-758B-B417-1E32-5C7D95D99CBF}
				//Компьютер\HKEY_CLASSES_ROOT\CLSID\{40E27B1D-758B-B417-1E32-5C7D95D99CBF}
				LPCWSTR guid = L"CLSID\\{40E27B1D-758B-B417-1E32-5C7D95D99CBF}";
				HKEY key = nullptr;
				LSTATUS status = RegOpenKeyExW(HKEY_CLASSES_ROOT, guid, 0, KEY_READ | KEY_WOW64_64KEY, &key);
				bool test = status == ERROR_FILE_NOT_FOUND;*/

				LPCWSTR guid = L"CLSID\\{A3FCE0F5-3493-419F-958A-ABA1250EC20B}";
				HKEY key = nullptr;
				LSTATUS status = RegOpenKeyExW(HKEY_CLASSES_ROOT, guid, 0, KEY_READ, &key);
				bool test = status == ERROR_FILE_NOT_FOUND;
				if (status != ERROR_SUCCESS) {
					throw gcnew ApplicationException("OBS Virtual Camera device not found! Please install OBS Virtual Camera.");
				}
				switch (fourcc)
				{
				case FourCC::FOURCC_RAW:
					break;
				case FourCC::FOURCC_24BG:
					break;
				case FourCC::FOURCC_J400:
					break;
				case FourCC::FOURCC_I420:
					break;
				case FourCC::FOURCC_NV12:
					break;
				case FourCC::FOURCC_YUY2:
					break;
				case FourCC::FOURCC_UYVY:
					break;
				default:
					throw gcnew ArgumentException("Bad fourcc format");
				}
				vo_ = new core::video_output(width, height, fps, (uint32_t)fourcc);
				width_ = width;
				height_ = height;
				fps_ = fps;
				fourcc_ = fourcc;
			}
			~VirtualOutput() {
				if (vo_ != nullptr) {
					delete vo_;
				}
			};
			!VirtualOutput() {
				if (vo_ != nullptr) {
					delete vo_;
				}
			}
			void Send(array<Byte>^ image);
			void Close() { vo_->stop(); };

			property int Width {
				int get() {
					return (int)width_;
				}
			}
			property int Height {
				int get() {
					return (int)height_;
				}
			}
			property int Fps {
				int get() {
					return (int)fps_;
				}
			}
			property FourCC Fourcc {
				FourCC get() {
					return fourcc_;
				}
			}
			property bool Running {
				bool get() {
					return vo_->is_running();
				}
			}
	};
}
