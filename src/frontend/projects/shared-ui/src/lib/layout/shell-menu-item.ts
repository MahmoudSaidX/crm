/** One entry in the application sidebar menu. Groups carry `items`, leaves carry `routerLink`. */
export interface ShellMenuItem {
  readonly label: string;
  readonly icon?: string;
  readonly routerLink?: string;
  readonly exact?: boolean;
  readonly items?: readonly ShellMenuItem[];
}
