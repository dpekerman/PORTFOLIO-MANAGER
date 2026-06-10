import { ChangeDetectionStrategy, Component } from "@angular/core";
import { MatIconModule } from "@angular/material/icon";
import { MatToolbarModule } from "@angular/material/toolbar";
import { RouterOutlet } from "@angular/router";

@Component({
  selector: "app-root",
  imports: [RouterOutlet, MatToolbarModule, MatIconModule],
  templateUrl: "./app.component.html",
  styleUrl: "./app.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppComponent {}
