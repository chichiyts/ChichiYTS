import libtorrent as lt
import json
from io import BytesIO
from zipfile import ZipFile
from rarfile import RarFile
import gzip
import psutil
import threading
import _sp
import _mp
import chardet
import sys
import time
# import traceback

CHICHI_PID = 0
CHECK_FOR_EXIT_INTERVAL = 1  # seconds
MAX_PARALLEL_REQUESTS = 5

PLAYABLE_MIME_TYPES = {
    'mp4': 'video/mp4',
    'avi': 'video/x-msvideo',
    'mkv': 'video/webm'
}

_input = {
    'position': 0,
}

_output = {
    'piece_size': 0,
    'download_rate': 0,
    'pieces': []
}

def download(link, storage_path):
    bencode = _mp.get_torrent_bytes(link)
    ti = lt.torrent_info(lt.bdecode(bencode))
    piece_size = ti.piece_length()
    files = ti.files()
    file_index = -1
    file_size = -1
    # get playable file with max size
    for i in range(ti.num_files()):
        ext = get_file_extension(files.file_name(i))
        if ext in PLAYABLE_MIME_TYPES:
            size = files.file_size(i)
            if size > file_size:
                file_index = i
                file_size = size
                mime_type = PLAYABLE_MIME_TYPES[ext]

    if file_index == -1:
        raise Exception(f'PLAYABLE FILE NOT FOUND: {link}')

    ses = lt.session({
        'alert_mask': lt.alert.category_t.storage_notification | lt.alert.category_t.stats_notification
    })

    range_first_byte = _input['position']
    range_last_byte = file_size - 1
    req = ti.map_file(file_index, range_first_byte, 0)
    min_inclusive_piece = req.piece
    max_exclusive_piece = ti.map_file(file_index, range_last_byte, 0).piece + 1
    offset = req.start
    print('piece_size: {} piece_range: {}->{} offset: {}'.format(piece_size, min_inclusive_piece, max_exclusive_piece, offset))
    current_piece = min_inclusive_piece
    last_piece = min(min_inclusive_piece + MAX_PARALLEL_REQUESTS, max_exclusive_piece)
    h = ses.add_torrent({'ti': ti, 'disabled_storage': 1})

    for i in range(min_inclusive_piece, last_piece):
        h.set_piece_deadline(i, i * 100, lt.torrent_handle.alert_when_available)

    while h.is_valid():
        alerts = ses.pop_alerts()
        for alert in alerts:
            if isinstance(alert, lt.read_piece_alert):
                print('{} -------- {}'.format(type(alert), alert.message()))
                if alert:
                    handle_buffer(alert.piece, alert.buffer, storage_path)

                    print('=====> request new piece:', last_piece, ' -current:', current_piece, ' -buffers:', len(_output['pieces']))
                    h.set_piece_deadline(last_piece, last_piece * 100, lt.torrent_handle.alert_when_available)
                    last_piece += 1
                else:
                    print('alert None???')
            elif isinstance(alert, lt.stats_alert):
                # print(alert.interval, '-', alert.transferred[lt.stats_channel.download_payload])
                _output['download_rate'] = alert.transferred[lt.stats_channel.download_payload] * 1000 // alert.interval
            else:
                print('{} - {}'.format(type(alert), alert.message()))

        ses.wait_for_alert(1000)

    print('============ END STREAM {} ========='.format(stream_count))


def get_file_extension(file_name):
    idx = file_name.rfind('.')
    return '' if idx == -1 else file_name[idx + 1:].lower()


def handle_buffer(piece, buffer, storage_path):
    with open(f'{storage_path}\\{piece}.dat', 'wb') as f:
        f.write(buffer)

    _output['pieces'].append(piece)


def check_for_exit():
    threading.Timer(CHECK_FOR_EXIT_INTERVAL, check_for_exit).start()
    # print('check for exit ...')
    try:
        psutil.Process(CHICHI_PID)
    except:
        import os
        os._exit(0)


def main(pid, storage_path, link):
    global CHICHI_PID
    if pid:
        CHICHI_PID = pid
        check_for_exit()

    download(link, storage_path)
