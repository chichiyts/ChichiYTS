import libtorrent as lt
from json import load, dumps
from io import BytesIO
from zipfile import ZipFile
from rarfile import RarFile
import gzip
import psutil
from threading import Timer
import _sp
import _mp
import chardet
import sys
import time
from os.path import getmtime, exists
from pathlib import Path
from math import ceil
import traceback

class Chichi:
    REQUEST_INTERVAL = 0.2
    MOVIE_INPUT_INTERVAL = 0.5 # seconds
    MOVIE_OUTPUT_INTERVAL = 1 # seconds
    CHECK_FOR_EXIT_INTERVAL = 1  # seconds
    MAX_PARALLEL_REQUESTS = 5

    PLAYABLE_MIME_TYPES = {
        'mp4': 'video/mp4',
        'avi': 'video/x-msvideo',
        'mkv': 'video/webm'
    }

    def __init__(self, pid, storage_path):
        self.pid = pid
        self.storage_path = storage_path
        self.movie_input_path = f'{storage_path}/movie/movie_input.qza'
        self.movie_output_path = f'{storage_path}/movie/movie_output.qza'
        self.request_path = f'{storage_path}/requests/request.qza'

        self.movie_input_modified = time.time()
        self.request_modified = None

        self.downloading = False
        self.allow_run = True

        self.ti = None
        self.file_index = None
        self.ses = lt.session({
            'alert_mask': lt.alert.category_t.storage_notification | lt.alert.category_t.stats_notification
        })

        self.movie_input = {
            'link': None,
            'position': None
        }

        if exists(self.movie_output_path):
            with open(self.movie_output_path) as f:
                self.movie_output = load(f)
        else:
            self.movie_output = {
                'file_size': None,
                'piece_size': None,
                'offset': 0,
                'mime_type': None,
                'download_rate': 0,
                'pieces': []
            }

    def process_movie_input(self):
        Timer(Chichi.MOVIE_INPUT_INTERVAL, self.process_movie_input).start()
        if not self.allow_run:
            return None

        try:
            modified = getmtime(self.movie_input_path)
            if modified <= self.movie_input_modified:
                return None

            with open(self.movie_input_path) as f:
                movie_input = load(f)

            link = movie_input.get('link')
            position = movie_input.get('position')
            if (link != self.movie_input.get('link')) or (position != self.movie_input.get('position')) or (position == 0):
                print('new movie input:', movie_input)
                self.allow_run = False
                print('stop downloading...')
                while self.downloading:
                    time.sleep(0.3)

                if link != self.movie_input.get('link'):
                    bencode = _mp.get_torrent_bytes(link)
                    ti = lt.torrent_info(lt.bdecode(bencode))
                    piece_size = ti.piece_length()
                    files = ti.files()
                    file_index = -1
                    file_size = -1
                    # get playable file with max size
                    for i in range(ti.num_files()):
                        ext = self.get_file_extension(files.file_name(i))
                        if ext in Chichi.PLAYABLE_MIME_TYPES:
                            size = files.file_size(i)
                            if size > file_size:
                                file_index = i
                                file_size = size
                                mime_type = Chichi.PLAYABLE_MIME_TYPES[ext]

                    if file_index == -1:
                        raise Exception(f'PLAYABLE FILE NOT FOUND: {link}')

                    for i in range(ceil(ceil(file_size/piece_size) / 100)):
                        Path(f'{self.storage_path}\\buffers\\{i}').mkdir(parents=True, exist_ok=True)

                    req = ti.map_file(file_index, 0, 0)
                    self.ti = ti
                    self.file_index = file_index
                    self.movie_input['link'] = link
                    self.movie_output['mime_type'] = mime_type
                    self.movie_output['file_size'] = file_size
                    self.movie_output['piece_size'] = piece_size
                    self.movie_output['download_rate'] = 0
                    self.movie_output['pieces'] = []
                    self.movie_output['offset'] = req.piece * piece_size + req.start

                if position != self.movie_input.get('position'):
                    self.movie_input['position'] = position

                self.movie_input_modified = modified
                self.allow_run = True
                self.download()

        except FileNotFoundError:
            pass

    def process_movie_output(self):
        Timer(Chichi.MOVIE_OUTPUT_INTERVAL, self.process_movie_output).start()
        if not self.downloading:
            return None


        # print('writing output...', dumps(self.movie_output))
        with open(self.movie_output_path, 'w') as f:
            f.write(dumps(self.movie_output))

    def process_request(self):
        Timer(Chichi.REQUEST_INTERVAL, self.process_request).start()
        try:
            modified = getmtime(self.request_path)
            if modified == self.request_modified:
                return None

            self.request_modified = modified

            with open(self.request_path) as f:
                request = load(f)

            print('new request:', request)
            request_type = request.get('type')
            out = request.get('out')
            res = ''
            if request_type == 'movies':
                query = request.get('query')
                res = _mp.get_movies(query)
                # print(res)
            elif request_type == 'subtitle_data':
                year = request.get('year')
                title = request.get('title')
                limit = request.get('limit')
                res = _sp.get_subtitle_data(year, title, limit)
            elif request_type == 'subtitles':
                link = request.get('link')
                limit = request.get('limit')
                res = _sp.get_subtitles(link, limit)
            elif request_type == 'subtitle':
                link = request.get('link')
                print('requests', link)
                content_type, content = _sp.get_subtitle(link)
                content_raw = None
                if 'zip' in content_type:
                    zip_file = ZipFile(BytesIO(content))
                    for file_name in zip_file.namelist():
                        ext = file_name[-4:].lower()
                        if ext in ['.srt', '.ass', '.smi']:
                            print(file_name)

                            content_raw = zip_file.open(file_name).read()
                            break
                        else:
                            print('ignore', file_name)
                elif 'rar' in content_type:
                    rar_file = RarFile(BytesIO(content))
                    for file_name in rar_file.namelist():
                        ext = file_name[-4:].lower()
                        if ext in ['.srt', '.ass', '.smi']:
                            print(file_name)

                            content_raw = rar_file.open(file_name).read()
                            break
                        else:
                            print('ignore', file_name)

                if content_raw:
                    encoding = chardet.detect(content_raw).get('encoding')
                    print(encoding)
                    if encoding.lower() != 'utf-8-sig':  # must be utf-8-sig
                        print('convert', encoding, '--> utf-8-sig')
                        content_raw = content_raw.decode(encoding).encode('utf-8-sig')
                    with open(f'{self.storage_path}\\responses\\{out}', 'wb') as f:
                        f.write(content_raw)
                else:
                    print('no subtitle', link)

                return None
            elif request_type == 'unregister':
                self.allow_run = False
                print('stop downloading...')
                while self.downloading:
                    time.sleep(0.3)

                if self.ti:
                    h = self.ses.find_torrent(self.ti.info_hash())
                    if h and h.is_valid():
                        self.ses.remove_torrent(h)

                self.allow_run = True
                return None  # stop

            with open(f'{self.storage_path}\\responses\\{out}', 'w') as f:
                f.write(dumps(res))

        except FileNotFoundError:
            pass
        except Exception as e:
            traceback.print_exc()
            with open(f'{self.storage_path}\\responses\\{out}', 'w') as f:
                f.write(str(e))

    def download(self):
        self.downloading = True
        try:
            ses = self.ses
            h = ses.add_torrent({'ti': self.ti, 'disabled_storage': 1})
            movie_output = self.movie_output
            pieces = movie_output['pieces']
            file_size = movie_output['file_size']
            piece_size = movie_output['piece_size']

            range_first_byte = self.movie_input['position']
            range_last_byte = file_size - 1
            req = self.ti.map_file(self.file_index, range_first_byte, 0)
            min_inclusive_piece = req.piece
            max_exclusive_piece = self.ti.map_file(self.file_index, range_last_byte, 0).piece + 1
            print('piece_size: {} piece_range: {}->{}'.format(piece_size, min_inclusive_piece, max_exclusive_piece))

            requested_pieces = set()
            last_piece = min_inclusive_piece
            for i in range(self.MAX_PARALLEL_REQUESTS):
                last_piece = self.poll_piece(last_piece, pieces)
                if last_piece < max_exclusive_piece:
                    print(f'=====> request new piece: {last_piece}')
                    h.set_piece_deadline(last_piece, last_piece * 100, lt.torrent_handle.alert_when_available)
                    requested_pieces.add(last_piece)
                    last_piece += 1

            count = 0
            while self.allow_run and count < len(requested_pieces):
                alerts = ses.pop_alerts()
                for alert in alerts:
                    if isinstance(alert, lt.read_piece_alert):
                        print('{} -------- {}'.format(type(alert), alert.message()))
                        if alert.handle == h:
                            self.handle_buffer(alert.piece, alert.buffer)
                            pieces.append(alert.piece)
                            if alert.piece in requested_pieces:
                                count += 1

                                last_piece = self.poll_piece(last_piece, pieces)
                                if last_piece < max_exclusive_piece:
                                    print(f'=====> request new piece: {last_piece} -done: {count}/{len(requested_pieces)}')
                                    h.set_piece_deadline(last_piece, last_piece * 100, lt.torrent_handle.alert_when_available)
                                    requested_pieces.add(last_piece)
                                    last_piece += 1
                        else:
                            print('  --> old torrent. ignored')
                    elif isinstance(alert, lt.stats_alert):
                        # print(alert.interval, '-', alert.transferred[lt.stats_channel.download_payload])
                        movie_output['download_rate'] = alert.transferred[lt.stats_channel.download_payload] * 1000 // alert.interval
                    else:
                        print('{} - {}'.format(type(alert), alert.message()))

                ses.wait_for_alert(1000)

            print('============ END STREAM =========')
        finally:
            self.downloading = False

    def get_file_extension(self, file_name):
        idx = file_name.rfind('.')
        return '' if idx == -1 else file_name[idx + 1:].lower()

    def handle_buffer(self, piece, buffer):
        while True:
            try:
                with open(f'{self.storage_path}\\buffers\\{piece//100}\\{piece}.buf', 'wb') as f:
                    f.write(buffer)

                break
            except:  # reading
                time.sleep(0.1)

    def poll_piece(self, current, existed_pieces):
        while current in existed_pieces:
            current += 1

        return current

    def check_for_exit(self):
        Timer(Chichi.CHECK_FOR_EXIT_INTERVAL, self.check_for_exit).start()
        # print('check for exit ...')
        try:
            psutil.Process(self.pid)
        except:
            import os
            os._exit(0)

    def run(self):
        self.check_for_exit()
        self.process_request()
        self.process_movie_output()
        self.process_movie_input()
