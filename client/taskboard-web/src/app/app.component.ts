import { Component, inject, OnInit } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { BOARD_CLIENT } from './core/grpc/grpc-clients.provider';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit {
  title = 'taskboard-web';

  // TEMPORARY: proves the Angular -> gRPC-Web -> API wire-up end to end
  // (CORS, Kestrel protocols, transport baseUrl) before the SignalStore step
  // replaces this with real state management.
  private readonly boardClient = inject(BOARD_CLIENT);

  ngOnInit(): void {
    this.boardClient.listBoards({}).response
      .then((res) => console.log('[gRPC-Web smoke test] ListBoards succeeded:', res.boards))
      .catch((err) => console.error('[gRPC-Web smoke test] ListBoards failed:', err));
  }
}
